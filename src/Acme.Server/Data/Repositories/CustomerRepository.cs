using System.Data.Common;
using Acme.Server.Data.Dialect;
using Acme.Server.Domain;
using Insight.Database;

namespace Acme.Server.Data.Repositories;

public interface ICustomerRepository
{
    Task<PagedResult<Customer>> ListAsync(DbConnection connection, string? search, PageRequest page);

    Task<Customer?> GetAsync(DbConnection connection, long id);

    Task<bool> ExistsByEmailAsync(DbConnection connection, string email, long? excludeId);

    /// <summary>All customers by last then first name -- the order form's dropdown.</summary>
    Task<IReadOnlyList<Customer>> ForSelectionAsync(DbConnection connection);

    Task<long> CountAsync(DbConnection connection);

    Task<long> InsertAsync(DbConnection connection, Customer customer);

    Task UpdateAsync(DbConnection connection, Customer customer);

    Task DeleteAsync(DbConnection connection, long id);
}

public sealed class CustomerRepository(ISqlDialect dialect, TimeProvider clock) : ICustomerRepository
{
    private const string Columns = """
        c.id                  AS Id,
        c.first_name          AS FirstName,
        c.last_name           AS LastName,
        c.email               AS Email,
        c.phone               AS Phone,
        c.address_line1       AS AddressLine1,
        c.address_line2       AS AddressLine2,
        c.address_city        AS AddressCity,
        c.address_state       AS AddressState,
        c.address_postal_code AS AddressPostalCode,
        c.address_country     AS AddressCountry,
        c.created_at          AS CreatedAt,
        c.updated_at          AS UpdatedAt
        """;

    public async Task<PagedResult<Customer>> ListAsync(DbConnection connection, string? search, PageRequest page)
    {
        var filtered = !string.IsNullOrWhiteSpace(search);

        var where = filtered
            ? """
              WHERE lower(c.first_name) LIKE lower(@Term)
                 OR lower(c.last_name)  LIKE lower(@Term)
                 OR lower(c.email)      LIKE lower(@Term)
              """
            : string.Empty;

        // See ProductRepository.ListAsync: a typed object of constant shape, not a
        // dictionary.
        var parameters = new { Term = filtered ? SqlHelpers.Contains(search!) : null };

        var total = await connection.ExecuteScalarSqlAsync<long>(
            $"SELECT count(*) FROM customers c {where}", parameters);

        if (total == 0)
        {
            return new PagedResult<Customer>([], page.Page, page.Size, 0);
        }

        SortMap.Customers.TryResolveColumn(page.SortProperty, out var column);
        var direction = page.Descending ? "DESC" : "ASC";

        var sql = dialect.Paginate(
            $"""
             SELECT
             {Columns}
             FROM customers c
             {where}
             ORDER BY {column} {direction}, c.id ASC
             """,
            page.Offset,
            page.Size);

        var rows = await connection.QuerySqlAsync<Customer>(sql, parameters);
        return new PagedResult<Customer>([.. rows], page.Page, page.Size, total);
    }

    public async Task<Customer?> GetAsync(DbConnection connection, long id) =>
        await connection.SingleSqlAsync<Customer>(
            $"SELECT {Columns} FROM customers c WHERE c.id = @Id",
            new { Id = id });

    public async Task<bool> ExistsByEmailAsync(DbConnection connection, string email, long? excludeId)
    {
        var exclusion = excludeId is null ? string.Empty : "AND c.id <> @ExcludeId";
        var count = await connection.ExecuteScalarSqlAsync<long>(
            $"SELECT count(*) FROM customers c WHERE lower(c.email) = lower(@Email) {exclusion}",
            new { Email = email, ExcludeId = excludeId ?? 0 });

        return count > 0;
    }

    public async Task<IReadOnlyList<Customer>> ForSelectionAsync(DbConnection connection)
    {
        var rows = await connection.QuerySqlAsync<Customer>(
            $"SELECT {Columns} FROM customers c ORDER BY c.last_name ASC, c.first_name ASC",
            new { });

        return [.. rows];
    }

    public Task<long> CountAsync(DbConnection connection) =>
        connection.ExecuteScalarSqlAsync<long>("SELECT count(*) FROM customers c", new { });

    public Task<long> InsertAsync(DbConnection connection, Customer customer)
    {
        customer.CreatedAt = clock.GetLocalNow().DateTime;
        customer.UpdatedAt = null;

        var sql = dialect.InsertReturningId(
            """
            INSERT INTO customers (first_name, last_name, email, phone,
                                   address_line1, address_line2, address_city,
                                   address_state, address_postal_code, address_country,
                                   created_at, updated_at)
            VALUES (@FirstName, @LastName, @Email, @Phone,
                    @AddressLine1, @AddressLine2, @AddressCity,
                    @AddressState, @AddressPostalCode, @AddressCountry,
                    @CreatedAt, @UpdatedAt)
            """);

        return connection.ExecuteScalarSqlAsync<long>(sql, customer);
    }

    public Task UpdateAsync(DbConnection connection, Customer customer)
    {
        customer.UpdatedAt = clock.GetLocalNow().DateTime;

        return connection.ExecuteSqlAsync(
            """
            UPDATE customers
               SET first_name          = @FirstName,
                   last_name           = @LastName,
                   email               = @Email,
                   phone               = @Phone,
                   address_line1       = @AddressLine1,
                   address_line2       = @AddressLine2,
                   address_city        = @AddressCity,
                   address_state       = @AddressState,
                   address_postal_code = @AddressPostalCode,
                   address_country     = @AddressCountry,
                   updated_at          = @UpdatedAt
             WHERE id = @Id
            """,
            customer);
    }

    public Task DeleteAsync(DbConnection connection, long id) =>
        connection.ExecuteSqlAsync("DELETE FROM customers WHERE id = @Id", new { Id = id });
}
