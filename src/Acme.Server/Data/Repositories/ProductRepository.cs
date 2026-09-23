using System.Data.Common;
using Acme.Server.Data.Dialect;
using Acme.Server.Domain;
using Insight.Database;

namespace Acme.Server.Data.Repositories;

public interface IProductRepository
{
    Task<PagedResult<Product>> ListAsync(DbConnection connection, string? search, PageRequest page);

    Task<Product?> GetAsync(DbConnection connection, long id);

    /// <summary>
    /// Case-insensitive existence check, optionally ignoring one row so an update can
    /// keep its own SKU. The database constraint is case-sensitive; this check is the
    /// one that produces a field-level error rather than a 500.
    /// </summary>
    Task<bool> ExistsBySkuAsync(DbConnection connection, string sku, long? excludeId);

    /// <summary>Active products by name -- the order form's product dropdown.</summary>
    Task<IReadOnlyList<Product>> ActiveAsync(DbConnection connection);

    Task<long> CountAsync(DbConnection connection);

    Task<long> InsertAsync(DbConnection connection, Product product);

    Task UpdateAsync(DbConnection connection, Product product);

    Task DeleteAsync(DbConnection connection, long id);
}

public sealed class ProductRepository(ISqlDialect dialect, TimeProvider clock) : IProductRepository
{
    /// <summary>
    /// Columns are aliased to the property names explicitly rather than relying on a
    /// global Insight <c>ColumnMapping</c> rule to strip underscores: we write all the
    /// SQL here anyway, and an explicit alias has no startup-ordering hazard.
    /// </summary>
    private const string Columns = """
        p.id             AS Id,
        p.sku            AS Sku,
        p.name           AS Name,
        p.description    AS Description,
        p.price          AS Price,
        p.stock_quantity AS StockQuantity,
        p.active         AS Active,
        p.created_at     AS CreatedAt,
        p.updated_at     AS UpdatedAt
        """;

    public async Task<PagedResult<Product>> ListAsync(DbConnection connection, string? search, PageRequest page)
    {
        var filtered = !string.IsNullOrWhiteSpace(search);

        // Matches findByNameContainingIgnoreCaseOrSkuContainingIgnoreCase.
        var where = filtered
            ? "WHERE lower(p.name) LIKE lower(@Term) OR lower(p.sku) LIKE lower(@Term)"
            : string.Empty;

        // A typed object, always with the same shape, even when the SQL has no @Term
        // to bind it to -- Insight only binds the parameters it finds in the text, and
        // its dictionary path has a caching bug (see SqlHelpers.InClause).
        var parameters = new { Term = filtered ? SqlHelpers.Contains(search!) : null };

        var total = await connection.ExecuteScalarSqlAsync<long>(
            $"SELECT count(*) FROM products p {where}", parameters);

        if (total == 0)
        {
            return new PagedResult<Product>([], page.Page, page.Size, 0);
        }

        SortMap.Products.TryResolveColumn(page.SortProperty, out var column);
        var direction = page.Descending ? "DESC" : "ASC";

        // The id tiebreaker is new: without it, paging over rows with equal sort keys
        // is not deterministic and a row can appear on two pages or neither.
        var sql = dialect.Paginate(
            $"""
             SELECT
             {Columns}
             FROM products p
             {where}
             ORDER BY {column} {direction}, p.id ASC
             """,
            page.Offset,
            page.Size);

        var rows = await connection.QuerySqlAsync<Product>(sql, parameters);
        return new PagedResult<Product>([.. rows], page.Page, page.Size, total);
    }

    public async Task<Product?> GetAsync(DbConnection connection, long id) =>
        await connection.SingleSqlAsync<Product>(
            $"SELECT {Columns} FROM products p WHERE p.id = @Id",
            new { Id = id });

    public async Task<bool> ExistsBySkuAsync(DbConnection connection, string sku, long? excludeId)
    {
        var exclusion = excludeId is null ? string.Empty : "AND p.id <> @ExcludeId";
        var count = await connection.ExecuteScalarSqlAsync<long>(
            $"SELECT count(*) FROM products p WHERE lower(p.sku) = lower(@Sku) {exclusion}",
            new { Sku = sku, ExcludeId = excludeId ?? 0 });

        return count > 0;
    }

    public async Task<IReadOnlyList<Product>> ActiveAsync(DbConnection connection)
    {
        var rows = await connection.QuerySqlAsync<Product>(
            $"SELECT {Columns} FROM products p WHERE p.active = @Active ORDER BY p.name ASC",
            new { Active = true });

        return [.. rows];
    }

    public Task<long> CountAsync(DbConnection connection) =>
        connection.ExecuteScalarSqlAsync<long>("SELECT count(*) FROM products p", new { });

    public Task<long> InsertAsync(DbConnection connection, Product product)
    {
        product.CreatedAt = clock.GetLocalNow().DateTime;
        product.UpdatedAt = null;

        var sql = dialect.InsertReturningId(
            """
            INSERT INTO products (sku, name, description, price, stock_quantity, active, created_at, updated_at)
            VALUES (@Sku, @Name, @Description, @Price, @StockQuantity, @Active, @CreatedAt, @UpdatedAt)
            """);

        return connection.ExecuteScalarSqlAsync<long>(sql, product);
    }

    public Task UpdateAsync(DbConnection connection, Product product)
    {
        product.UpdatedAt = clock.GetLocalNow().DateTime;

        return connection.ExecuteSqlAsync(
            """
            UPDATE products
               SET sku            = @Sku,
                   name           = @Name,
                   description    = @Description,
                   price          = @Price,
                   stock_quantity = @StockQuantity,
                   active         = @Active,
                   updated_at     = @UpdatedAt
             WHERE id = @Id
            """,
            product);
    }

    public Task DeleteAsync(DbConnection connection, long id) =>
        connection.ExecuteSqlAsync("DELETE FROM products WHERE id = @Id", new { Id = id });
}
