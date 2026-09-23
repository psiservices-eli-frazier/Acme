using System.Data.Common;
using Acme.Server.Data.Dialect;
using Acme.Server.Domain;
using Insight.Database;

namespace Acme.Server.Data.Repositories;

public interface IOrderRepository
{
    /// <summary>
    /// Paged orders with their customer joined. Lines are not loaded.
    ///
    /// <paramref name="customerId"/> is a real filter, unlike the Java screens, which
    /// linked a customer's orders by pasting their email into the search box.
    /// </summary>
    Task<PagedResult<Order>> ListAsync(
        DbConnection connection,
        string? search,
        long? customerId,
        PageRequest page);

    /// <summary>The order with its customer, its lines, and each line's product.</summary>
    Task<Order?> GetDetailAsync(DbConnection connection, long id);

    Task<bool> ExistsByOrderNumberAsync(DbConnection connection, string orderNumber);

    Task<long> CountAsync(DbConnection connection);

    /// <summary>Guards customer deletion.</summary>
    Task<long> CountByCustomerAsync(DbConnection connection, long customerId);

    /// <summary>Guards product deletion.</summary>
    Task<long> CountLinesByProductAsync(DbConnection connection, long productId);

    /// <summary>
    /// Order totals for one page of the list, keyed by order id. Orders with no lines
    /// are absent from the result; the client renders those as 0.00.
    /// </summary>
    Task<IReadOnlyDictionary<long, decimal>> GetTotalsAsync(
        DbConnection connection,
        IReadOnlyCollection<long> orderIds);

    Task<long> InsertAsync(DbConnection connection, Order order);

    Task UpdateAsync(DbConnection connection, Order order);

    Task DeleteAsync(DbConnection connection, long id);

    Task DeleteLinesAsync(DbConnection connection, long orderId);

    Task InsertLineAsync(DbConnection connection, long orderId, OrderItem item);
}

public sealed class OrderRepository(ISqlDialect dialect, TimeProvider clock) : IOrderRepository
{
    private const string OrderColumns = """
        o.id           AS Id,
        o.order_number AS OrderNumber,
        o.customer_id  AS CustomerId,
        o.status       AS Status,
        o.ordered_at   AS OrderedAt,
        o.notes        AS Notes,
        o.created_at   AS CreatedAt,
        o.updated_at   AS UpdatedAt
        """;

    public async Task<PagedResult<Order>> ListAsync(
        DbConnection connection,
        string? search,
        long? customerId,
        PageRequest page)
    {
        var conditions = new List<string>();

        // Matches OrderRepository.search: one term against the order number and the
        // customer's first name, last name and email.
        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("""
                (   lower(o.order_number) LIKE lower(@Term)
                 OR lower(c.first_name)   LIKE lower(@Term)
                 OR lower(c.last_name)    LIKE lower(@Term)
                 OR lower(c.email)        LIKE lower(@Term))
                """);
        }

        if (customerId is not null)
        {
            conditions.Add("o.customer_id = @CustomerId");
        }

        var where = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join("\n  AND ", conditions);

        // See ProductRepository.ListAsync: a typed object of constant shape, not a
        // dictionary, whatever subset of it the SQL actually references.
        var parameters = new
        {
            Term = string.IsNullOrWhiteSpace(search) ? null : SqlHelpers.Contains(search),
            CustomerId = customerId,
        };

        var total = await connection.ExecuteScalarSqlAsync<long>(
            $"""
             SELECT count(*)
             FROM orders o
             JOIN customers c ON c.id = o.customer_id
             {where}
             """,
            parameters);

        if (total == 0)
        {
            return new PagedResult<Order>([], page.Page, page.Size, 0);
        }

        SortMap.Orders.TryResolveColumn(page.SortProperty, out var column);
        var direction = page.Descending ? "DESC" : "ASC";

        // The customer is joined here for the same reason the Java query carried an
        // @EntityGraph: the list screen shows the customer name, and nothing in this
        // stack lazy-loads.
        var sql = dialect.Paginate(
            $"""
             SELECT
             {OrderColumns},
             c.first_name AS CustomerFirstName,
             c.last_name  AS CustomerLastName,
             c.email      AS CustomerEmail
             FROM orders o
             JOIN customers c ON c.id = o.customer_id
             {where}
             ORDER BY {column} {direction}, o.id ASC
             """,
            page.Offset,
            page.Size);

        var rows = await connection.QuerySqlAsync<OrderListRow>(sql, parameters);
        return new PagedResult<Order>([.. rows.Select(row => row.ToOrder())], page.Page, page.Size, total);
    }

    public async Task<Order?> GetDetailAsync(DbConnection connection, long id)
    {
        // Two statements rather than one fetch-join. The Java version needed a single
        // query because Hibernate would otherwise lazy-load during rendering; here
        // there is no session to close and two explicit round trips are clearer than
        // relying on Insight's record-splitting heuristics. It is not an N+1 -- the
        // line count does not affect the number of queries.
        var header = await connection.SingleSqlAsync<OrderDetailRow>(
            $"""
             SELECT
             {OrderColumns},
             c.id                  AS CustomerId2,
             c.first_name          AS CustomerFirstName,
             c.last_name           AS CustomerLastName,
             c.email               AS CustomerEmail,
             c.phone               AS CustomerPhone,
             c.address_line1       AS CustomerAddressLine1,
             c.address_line2       AS CustomerAddressLine2,
             c.address_city        AS CustomerAddressCity,
             c.address_state       AS CustomerAddressState,
             c.address_postal_code AS CustomerAddressPostalCode,
             c.address_country     AS CustomerAddressCountry
             FROM orders o
             JOIN customers c ON c.id = o.customer_id
             WHERE o.id = @Id
             """,
            new { Id = id });

        if (header is null)
        {
            return null;
        }

        var lines = await connection.QuerySqlAsync<OrderItemRow>(
            """
            SELECT
            oi.id         AS Id,
            oi.order_id   AS OrderId,
            oi.product_id AS ProductId,
            oi.quantity   AS Quantity,
            oi.unit_price AS UnitPrice,
            p.sku         AS ProductSku,
            p.name        AS ProductName
            FROM order_items oi
            JOIN products p ON p.id = oi.product_id
            WHERE oi.order_id = @OrderId
            ORDER BY oi.id ASC
            """,
            new { OrderId = id });

        var order = header.ToOrder();
        order.Items = [.. lines.Select(line => line.ToOrderItem())];
        return order;
    }

    public async Task<bool> ExistsByOrderNumberAsync(DbConnection connection, string orderNumber)
    {
        var count = await connection.ExecuteScalarSqlAsync<long>(
            "SELECT count(*) FROM orders o WHERE lower(o.order_number) = lower(@OrderNumber)",
            new { OrderNumber = orderNumber });

        return count > 0;
    }

    public Task<long> CountAsync(DbConnection connection) =>
        connection.ExecuteScalarSqlAsync<long>("SELECT count(*) FROM orders o", new { });

    public Task<long> CountByCustomerAsync(DbConnection connection, long customerId) =>
        connection.ExecuteScalarSqlAsync<long>(
            "SELECT count(*) FROM orders o WHERE o.customer_id = @CustomerId",
            new { CustomerId = customerId });

    public Task<long> CountLinesByProductAsync(DbConnection connection, long productId) =>
        connection.ExecuteScalarSqlAsync<long>(
            "SELECT count(*) FROM order_items oi WHERE oi.product_id = @ProductId",
            new { ProductId = productId });

    public async Task<IReadOnlyDictionary<long, decimal>> GetTotalsAsync(
        DbConnection connection,
        IReadOnlyCollection<long> orderIds)
    {
        // `IN ()` is a syntax error on several engines, so never issue it.
        if (orderIds.Count == 0)
        {
            return new Dictionary<long, decimal>();
        }

        var fragment = SqlHelpers.InClause(orderIds);

        if (dialect.SupportsDecimalAggregate)
        {
            var aggregated = await connection.QuerySqlAsync<OrderTotalRow>(
                $"""
                 SELECT oi.order_id AS OrderId, SUM(oi.unit_price * oi.quantity) AS Total
                 FROM order_items oi
                 WHERE oi.order_id IN {fragment}
                 GROUP BY oi.order_id
                 """,
                new { });

            return aggregated.ToDictionary(row => row.OrderId, row => row.Total);
        }

        // SQLite stores money as a double, so summing in SQL would accumulate float
        // error. Reading each line converts to decimal first (which recovers the
        // intended 2dp value), and the sum is then exact.
        var lines = await connection.QuerySqlAsync<OrderLineAmountRow>(
            $"""
             SELECT oi.order_id AS OrderId, oi.unit_price AS UnitPrice, oi.quantity AS Quantity
             FROM order_items oi
             WHERE oi.order_id IN {fragment}
             """,
            new { });

        return lines
            .GroupBy(line => line.OrderId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.UnitPrice * line.Quantity));
    }

    public Task<long> InsertAsync(DbConnection connection, Order order)
    {
        order.CreatedAt = clock.GetLocalNow().DateTime;
        order.UpdatedAt = null;

        var sql = dialect.InsertReturningId(
            """
            INSERT INTO orders (order_number, customer_id, status, ordered_at, notes, created_at, updated_at)
            VALUES (@OrderNumber, @CustomerId, @Status, @OrderedAt, @Notes, @CreatedAt, @UpdatedAt)
            """);

        return connection.ExecuteScalarSqlAsync<long>(
            sql,
            new
            {
                order.OrderNumber,
                order.CustomerId,
                Status = order.Status.ToDbValue(),
                order.OrderedAt,
                order.Notes,
                order.CreatedAt,
                order.UpdatedAt,
            });
    }

    /// <summary>
    /// Updates the mutable header fields only. <c>order_number</c> and <c>ordered_at</c>
    /// are set once on create and are deliberately absent here.
    /// </summary>
    public Task UpdateAsync(DbConnection connection, Order order)
    {
        order.UpdatedAt = clock.GetLocalNow().DateTime;

        return connection.ExecuteSqlAsync(
            """
            UPDATE orders
               SET customer_id = @CustomerId,
                   status      = @Status,
                   notes       = @Notes,
                   updated_at  = @UpdatedAt
             WHERE id = @Id
            """,
            new
            {
                order.Id,
                order.CustomerId,
                Status = order.Status.ToDbValue(),
                order.Notes,
                order.UpdatedAt,
            });
    }

    public Task DeleteAsync(DbConnection connection, long id) =>
        connection.ExecuteSqlAsync("DELETE FROM orders WHERE id = @Id", new { Id = id });

    public Task DeleteLinesAsync(DbConnection connection, long orderId) =>
        connection.ExecuteSqlAsync("DELETE FROM order_items WHERE order_id = @OrderId", new { OrderId = orderId });

    public Task InsertLineAsync(DbConnection connection, long orderId, OrderItem item) =>
        connection.ExecuteSqlAsync(
            """
            INSERT INTO order_items (order_id, product_id, quantity, unit_price)
            VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice)
            """,
            new
            {
                OrderId = orderId,
                item.ProductId,
                item.Quantity,
                item.UnitPrice,
            });

    // ---- flat row types -------------------------------------------------------
    //
    // Joined queries map onto explicit flat rows rather than Insight's one-to-one
    // record splitting, which infers the boundary between two objects from the column
    // names. These are private, dumb, and impossible to misread.

    private class OrderRow
    {
        public long Id { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public long CustomerId { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime OrderedAt { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        protected Order ToOrderCore() => new()
        {
            Id = Id,
            OrderNumber = OrderNumber,
            CustomerId = CustomerId,
            Status = OrderStatusExtensions.FromDbValue(Status),
            OrderedAt = OrderedAt,
            Notes = Notes,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
    }

    private sealed class OrderListRow : OrderRow
    {
        public string CustomerFirstName { get; set; } = string.Empty;

        public string CustomerLastName { get; set; } = string.Empty;

        public string CustomerEmail { get; set; } = string.Empty;

        public Order ToOrder()
        {
            var order = ToOrderCore();
            order.Customer = new Customer
            {
                Id = CustomerId,
                FirstName = CustomerFirstName,
                LastName = CustomerLastName,
                Email = CustomerEmail,
            };
            return order;
        }
    }

    private sealed class OrderDetailRow : OrderRow
    {
        // Aliased CustomerId2 because CustomerId is already the order's own FK column.
        public long CustomerId2 { get; set; }

        public string CustomerFirstName { get; set; } = string.Empty;

        public string CustomerLastName { get; set; } = string.Empty;

        public string CustomerEmail { get; set; } = string.Empty;

        public string? CustomerPhone { get; set; }

        public string? CustomerAddressLine1 { get; set; }

        public string? CustomerAddressLine2 { get; set; }

        public string? CustomerAddressCity { get; set; }

        public string? CustomerAddressState { get; set; }

        public string? CustomerAddressPostalCode { get; set; }

        public string? CustomerAddressCountry { get; set; }

        public Order ToOrder()
        {
            var order = ToOrderCore();
            order.Customer = new Customer
            {
                Id = CustomerId2,
                FirstName = CustomerFirstName,
                LastName = CustomerLastName,
                Email = CustomerEmail,
                Phone = CustomerPhone,
                AddressLine1 = CustomerAddressLine1,
                AddressLine2 = CustomerAddressLine2,
                AddressCity = CustomerAddressCity,
                AddressState = CustomerAddressState,
                AddressPostalCode = CustomerAddressPostalCode,
                AddressCountry = CustomerAddressCountry,
            };
            return order;
        }
    }

    private sealed class OrderItemRow
    {
        public long Id { get; set; }

        public long OrderId { get; set; }

        public long ProductId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public string ProductSku { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public OrderItem ToOrderItem() => new()
        {
            Id = Id,
            OrderId = OrderId,
            ProductId = ProductId,
            Quantity = Quantity,
            UnitPrice = UnitPrice,
            Product = new Product { Id = ProductId, Sku = ProductSku, Name = ProductName },
        };
    }

    private sealed class OrderTotalRow
    {
        public long OrderId { get; set; }

        public decimal Total { get; set; }
    }

    private sealed class OrderLineAmountRow
    {
        public long OrderId { get; set; }

        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }
    }
}
