using System.Data.Common;
using Acme.Server.Contracts;
using Acme.Server.Data;
using Acme.Server.Data.Repositories;
using Acme.Server.Domain;
using Acme.Server.Security;

namespace Acme.Server.Services;

public sealed class ProductService(
    IDbConnectionFactory connections,
    IProductRepository products,
    IOrderRepository orders,
    IAccessGuard access)
{
    public async Task<PagedResult<Product>> ListAsync(string? search, PageRequest page, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await products.ListAsync(connection, search, page);
    }

    public async Task<Product> GetAsync(long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await GetOrThrowAsync(connection, id);
    }

    public async Task<IReadOnlyList<Product>> ActiveAsync(CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await products.ActiveAsync(connection);
    }

    public async Task<long> CountAsync(CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await products.CountAsync(connection);
    }

    public async Task<Product> CreateAsync(ProductInput input, CancellationToken ct)
    {
        await access.RequireAsync(Policies.AdminOnly);

        await using var connection = await connections.OpenWithTransactionAsync(ct);
        await RequireUniqueSkuAsync(connection, input.Sku, excludeId: null);

        // Built fresh from whitelisted fields. The input type has no id, so there is
        // nothing a crafted request could point this write at.
        var product = new Product
        {
            Sku = input.Sku,
            Name = input.Name,
            Description = input.Description,
            Price = input.Price!.Value,
            StockQuantity = input.StockQuantity!.Value,
            Active = input.Active,
        };

        product.Id = await products.InsertAsync(connection, product);
        connection.Commit();
        return product;
    }

    public async Task<Product> UpdateAsync(long id, ProductInput input, CancellationToken ct)
    {
        await access.RequireAsync(Policies.AdminOnly);

        await using var connection = await connections.OpenWithTransactionAsync(ct);

        // Not-found is reported before the uniqueness check, so editing a row that does
        // not exist is a 404 rather than a confusing duplicate-value error.
        var product = await GetOrThrowAsync(connection, id);
        await RequireUniqueSkuAsync(connection, input.Sku, excludeId: id);

        product.Sku = input.Sku;
        product.Name = input.Name;
        product.Description = input.Description;
        product.Price = input.Price!.Value;
        product.StockQuantity = input.StockQuantity!.Value;
        product.Active = input.Active;

        await products.UpdateAsync(connection, product);
        connection.Commit();
        return product;
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        await access.RequireAsync(Policies.AdminOnly);

        await using var connection = await connections.OpenWithTransactionAsync(ct);
        var product = await GetOrThrowAsync(connection, id);

        var lines = await orders.CountLinesByProductAsync(connection, id);
        if (lines > 0)
        {
            throw new EntityInUseException(
                $"{product.Name} cannot be deleted because it appears on {lines} order line(s). "
                + "Mark it inactive instead.");
        }

        await products.DeleteAsync(connection, id);
        connection.Commit();
    }

    private async Task<Product> GetOrThrowAsync(DbConnection connection, long id) =>
        await products.GetAsync(connection, id)
        ?? throw new NotFoundException($"No product exists with id {id}");

    /// <summary>
    /// Compared case-insensitively while the database constraint is case-sensitive.
    /// This check is the one that produces a tidy field error; the constraint is the
    /// backstop that would otherwise surface as a 500.
    /// </summary>
    private async Task RequireUniqueSkuAsync(DbConnection connection, string sku, long? excludeId)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return; // validation already reports the blank
        }

        if (await products.ExistsBySkuAsync(connection, sku, excludeId))
        {
            // PascalCase to match the keys the framework's own validation emits, so the
            // client can look up a field error one way regardless of which produced it.
            throw new DuplicateValueException("Sku", $"SKU '{sku}' is already used by another product");
        }
    }
}
