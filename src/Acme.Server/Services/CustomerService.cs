using System.Data.Common;
using Acme.Server.Contracts;
using Acme.Server.Data;
using Acme.Server.Data.Repositories;
using Acme.Server.Domain;
using Acme.Server.Security;

namespace Acme.Server.Services;

public sealed class CustomerService(
    IDbConnectionFactory connections,
    ICustomerRepository customers,
    IOrderRepository orders,
    IAccessGuard access)
{
    public async Task<PagedResult<Customer>> ListAsync(string? search, PageRequest page, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await customers.ListAsync(connection, search, page);
    }

    public async Task<Customer> GetAsync(long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await GetOrThrowAsync(connection, id);
    }

    public async Task<long> OrderCountAsync(long customerId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await orders.CountByCustomerAsync(connection, customerId);
    }

    public async Task<IReadOnlyList<Customer>> ForSelectionAsync(CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await customers.ForSelectionAsync(connection);
    }

    public async Task<long> CountAsync(CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await customers.CountAsync(connection);
    }

    public async Task<Customer> CreateAsync(CustomerInput input, CancellationToken ct)
    {
        await access.RequireAsync(Policies.AdminOnly);

        await using var connection = await connections.OpenWithTransactionAsync(ct);
        await RequireUniqueEmailAsync(connection, input.Email, excludeId: null);

        var customer = new Customer();
        Apply(customer, input);

        customer.Id = await customers.InsertAsync(connection, customer);
        connection.Commit();
        return customer;
    }

    public async Task<Customer> UpdateAsync(long id, CustomerInput input, CancellationToken ct)
    {
        await access.RequireAsync(Policies.AdminOnly);

        await using var connection = await connections.OpenWithTransactionAsync(ct);
        var customer = await GetOrThrowAsync(connection, id);
        await RequireUniqueEmailAsync(connection, input.Email, excludeId: id);

        Apply(customer, input);

        await customers.UpdateAsync(connection, customer);
        connection.Commit();
        return customer;
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        await access.RequireAsync(Policies.AdminOnly);

        await using var connection = await connections.OpenWithTransactionAsync(ct);
        var customer = await GetOrThrowAsync(connection, id);

        var orderCount = await orders.CountByCustomerAsync(connection, id);
        if (orderCount > 0)
        {
            throw new EntityInUseException(
                $"{customer.FullName} cannot be deleted because they have {orderCount} order(s). "
                + "Delete or reassign those orders first.");
        }

        await customers.DeleteAsync(connection, id);
        connection.Commit();
    }

    /// <summary>
    /// Copies the whitelisted fields. An omitted address block clears the address
    /// columns, matching the Java behaviour of replacing the whole embeddable.
    /// </summary>
    private static void Apply(Customer customer, CustomerInput input)
    {
        customer.FirstName = input.FirstName;
        customer.LastName = input.LastName;
        customer.Email = input.Email;
        customer.Phone = input.Phone;
        customer.AddressLine1 = input.Address?.Line1;
        customer.AddressLine2 = input.Address?.Line2;
        customer.AddressCity = input.Address?.City;
        customer.AddressState = input.Address?.State;
        customer.AddressPostalCode = input.Address?.PostalCode;
        customer.AddressCountry = input.Address?.Country;
    }

    private async Task<Customer> GetOrThrowAsync(DbConnection connection, long id) =>
        await customers.GetAsync(connection, id)
        ?? throw new NotFoundException($"No customer exists with id {id}");

    private async Task RequireUniqueEmailAsync(DbConnection connection, string email, long? excludeId)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return; // validation already reports the blank
        }

        if (await customers.ExistsByEmailAsync(connection, email, excludeId))
        {
            // PascalCase to match the keys the framework's own validation emits, so the
            // client can look up a field error one way regardless of which produced it.
            throw new DuplicateValueException("Email", $"Email '{email}' is already used by another customer");
        }
    }
}
