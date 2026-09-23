using System.Data.Common;
using System.Globalization;
using Acme.Server.Contracts;
using Acme.Server.Data;
using Acme.Server.Data.Repositories;
using Acme.Server.Domain;
using Acme.Server.Security;

namespace Acme.Server.Services;

public sealed class OrderService(
    IDbConnectionFactory connections,
    IOrderRepository orders,
    ICustomerRepository customers,
    IProductRepository products,
    IAccessGuard access,
    TimeProvider clock)
{
    private const int OrderNumberAttempts = 10;

    public async Task<PagedResult<Order>> ListAsync(
        string? search,
        long? customerId,
        PageRequest page,
        CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await orders.ListAsync(connection, search, customerId, page);
    }

    public async Task<IReadOnlyDictionary<long, decimal>> TotalsForAsync(
        IReadOnlyCollection<long> orderIds,
        CancellationToken ct)
    {
        if (orderIds.Count == 0)
        {
            return new Dictionary<long, decimal>();
        }

        await using var connection = await connections.OpenAsync(ct);
        return await orders.GetTotalsAsync(connection, orderIds);
    }

    public async Task<Order> GetDetailAsync(long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await GetDetailOrThrowAsync(connection, id);
    }

    public async Task<long> CountAsync(CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await orders.CountAsync(connection);
    }

    /// <summary>
    /// Orders are the one area STAFF may change. Deleting one is still admin-only --
    /// it discards the line items with it, so it destroys order history rather than
    /// correcting it.
    /// </summary>
    public async Task<Order> CreateAsync(OrderInput input, CancellationToken ct)
    {
        await access.RequireAsync(Policies.AdminOrStaff);

        await using var connection = await connections.OpenWithTransactionAsync(ct);

        var order = new Order
        {
            // Allocated before any foreign key is resolved: if the generator gives up,
            // it gives up without having touched customers or products.
            OrderNumber = await GenerateOrderNumberAsync(connection),
            OrderedAt = clock.GetLocalNow().DateTime,
        };

        // Nothing is booked yet, so every line takes its product's current price.
        await ApplyInputAsync(connection, order, input, bookedPrices: new Dictionary<long, decimal>());

        order.Id = await orders.InsertAsync(connection, order);
        foreach (var item in order.Items)
        {
            await orders.InsertLineAsync(connection, order.Id, item);
        }

        var created = await GetDetailOrThrowAsync(connection, order.Id);
        connection.Commit();
        return created;
    }

    public async Task<Order> UpdateAsync(long id, OrderInput input, CancellationToken ct)
    {
        await access.RequireAsync(Policies.AdminOrStaff);

        await using var connection = await connections.OpenWithTransactionAsync(ct);
        var order = await GetDetailOrThrowAsync(connection, id);

        // Preserve the price each product was already booked at. Without this, editing
        // an order would re-read the product's price and silently restate order history
        // at today's prices, defeating the point of the snapshot.
        //
        // Keyed by product id, not line id, and first-wins: if the same product appears
        // on two existing lines at different prices, the first line's price is the one
        // that carries. This is deliberate -- it is what the Java putIfAbsent did.
        var bookedPrices = new Dictionary<long, decimal>();
        foreach (var existing in order.Items)
        {
            bookedPrices.TryAdd(existing.ProductId, existing.UnitPrice);
        }

        await ApplyInputAsync(connection, order, input, bookedPrices);

        await orders.UpdateAsync(connection, order);

        // Lines are replaced wholesale rather than diffed. Line ids therefore do not
        // survive an edit, which is why the input carries none.
        await orders.DeleteLinesAsync(connection, order.Id);
        foreach (var item in order.Items)
        {
            await orders.InsertLineAsync(connection, order.Id, item);
        }

        var updated = await GetDetailOrThrowAsync(connection, order.Id);
        connection.Commit();
        return updated;
    }

    /// <summary>
    /// No in-use guard: nothing references an order, and its lines go with it.
    /// </summary>
    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        await access.RequireAsync(Policies.AdminOnly);

        await using var connection = await connections.OpenWithTransactionAsync(ct);
        _ = await GetDetailOrThrowAsync(connection, id);

        await orders.DeleteLinesAsync(connection, id);
        await orders.DeleteAsync(connection, id);
        connection.Commit();
    }

    /// <summary>
    /// Resolves the customer and the lines onto <paramref name="order"/>.
    ///
    /// Deliberately does not touch <c>OrderNumber</c> or <c>OrderedAt</c>: both are set
    /// once on create and an edit must leave them alone.
    /// </summary>
    private async Task ApplyInputAsync(
        DbConnection connection,
        Order order,
        OrderInput input,
        IReadOnlyDictionary<long, decimal> bookedPrices)
    {
        var customer = await customers.GetAsync(connection, input.CustomerId!.Value)
            ?? throw new NotFoundException($"No customer exists with id {input.CustomerId}");

        order.CustomerId = customer.Id;
        order.Customer = customer;
        order.Status = input.Status!.Value;
        order.Notes = input.Notes;

        var lines = new List<OrderItem>();
        foreach (var line in input.Items)
        {
            if (line?.ProductId is null)
            {
                continue; // blank row; validation reports it before we get here
            }

            var product = await products.GetAsync(connection, line.ProductId.Value)
                ?? throw new NotFoundException($"No product exists with id {line.ProductId}");

            // A product already on this order keeps its booked price even if the row
            // was removed and re-added in the same submit. A product that was not on
            // the order takes today's price.
            var unitPrice = bookedPrices.TryGetValue(product.Id, out var booked) ? booked : product.Price;

            lines.Add(new OrderItem
            {
                ProductId = product.Id,
                Product = product,
                Quantity = line.Quantity ?? 1,
                UnitPrice = unitPrice,
            });
        }

        order.Items = lines;
    }

    /// <summary>
    /// Allocates a human-friendly order number, retrying on the (unlikely) collision.
    /// The unique constraint on <c>order_number</c> remains the real guarantee -- the
    /// probe below is a time-of-check/time-of-use race that is acknowledged, not closed.
    /// </summary>
    private async Task<string> GenerateOrderNumberAsync(DbConnection connection)
    {
        var datePart = clock.GetLocalNow().DateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        for (var attempt = 0; attempt < OrderNumberAttempts; attempt++)
        {
            var candidate = $"BX-{datePart}-{Random.Shared.Next(10_000):0000}";
            if (!await orders.ExistsByOrderNumberAsync(connection, candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Could not allocate a unique order number after {OrderNumberAttempts} attempts");
    }

    private async Task<Order> GetDetailOrThrowAsync(DbConnection connection, long id) =>
        await orders.GetDetailAsync(connection, id)
        ?? throw new NotFoundException($"No order exists with id {id}");
}
