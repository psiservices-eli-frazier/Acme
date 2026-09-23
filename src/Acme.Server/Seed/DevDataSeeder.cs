using System.Data.Common;
using Acme.Server.Data;
using Acme.Server.Data.Repositories;
using Acme.Server.Domain;

namespace Acme.Server.Seed;

/// <summary>
/// Inserts the demo products, customers and orders on an empty database.
///
/// The values are identical to the Java <c>DevDataSeeder</c> -- same SKUs, prices,
/// addresses, order numbers and line quantities -- so the two applications can be run
/// side by side and compared screen for screen.
///
/// Seeded through the repositories rather than a SQL script, to stay engine-agnostic.
/// </summary>
public sealed class DevDataSeeder(
    IDbConnectionFactory connections,
    IProductRepository products,
    ICustomerRepository customers,
    IOrderRepository orders,
    TimeProvider clock,
    ILogger<DevDataSeeder> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        await using var connection = await connections.OpenWithTransactionAsync(ct);

        if (await products.CountAsync(connection) > 0
            || await customers.CountAsync(connection) > 0
            || await orders.CountAsync(connection) > 0)
        {
            return;
        }

        var widget = await AddProductAsync(connection,
            "BX-WIDGET-01", "Standard Widget", "The everyday widget. Steel body, 10mm thread.", 19.99m, 250);
        var widgetPro = await AddProductAsync(connection,
            "BX-WIDGET-02", "Widget Pro", "Hardened widget rated for continuous duty.", 34.50m, 80);
        var gizmo = await AddProductAsync(connection,
            "BX-GIZMO-01", "Gizmo Assembly", "Pre-assembled gizmo with mounting bracket.", 129.00m, 42);
        var sprocket = await AddProductAsync(connection,
            "BX-SPROCKET-01", "Sprocket, 12T", "Twelve-tooth sprocket, zinc plated.", 7.25m, 1000);

        var dana = await AddCustomerAsync(connection,
            "Dana", "Whitfield", "dana.whitfield@acme-supply.example", "+1-312-555-0142",
            "400 W Erie St", "Suite 210", "Chicago", "IL", "60654");
        var priya = await AddCustomerAsync(connection,
            "Priya", "Raghunathan", "p.raghunathan@northwind.example", "+1-206-555-0188",
            "1120 Pike St", null, "Seattle", "WA", "98101");
        var marcus = await AddCustomerAsync(connection,
            "Marcus", "Oyelaran", "marcus@harborworks.example", "+1-617-555-0110",
            "88 Seaport Blvd", "Floor 3", "Boston", "MA", "02210");

        // Seeded order numbers use a BX-SEED-nnnn shape, distinct from the generated
        // BX-yyyyMMdd-NNNN, so demo rows are obvious at a glance.
        await AddOrderAsync(connection, "BX-SEED-0001", dana, OrderStatus.Delivered, daysAgo: 21,
            "Standing monthly resupply.",
            (widget, 40), (sprocket, 120));

        await AddOrderAsync(connection, "BX-SEED-0002", priya, OrderStatus.Shipped, daysAgo: 6,
            "Rush order - expedited freight requested.",
            (gizmo, 3), (widgetPro, 10));

        await AddOrderAsync(connection, "BX-SEED-0003", marcus, OrderStatus.New, daysAgo: 1,
            null,
            (widgetPro, 25));

        connection.Commit();
        logger.LogInformation("Seeded demo products, customers and orders.");
    }

    private async Task<Product> AddProductAsync(
        DbConnection connection, string sku, string name, string description, decimal price, int stock)
    {
        var product = new Product
        {
            Sku = sku,
            Name = name,
            Description = description,
            Price = price,
            StockQuantity = stock,
            Active = true,
        };

        product.Id = await products.InsertAsync(connection, product);
        return product;
    }

    private async Task<Customer> AddCustomerAsync(
        DbConnection connection,
        string firstName,
        string lastName,
        string email,
        string phone,
        string line1,
        string? line2,
        string city,
        string state,
        string postalCode)
    {
        var customer = new Customer
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Phone = phone,
            AddressLine1 = line1,
            AddressLine2 = line2,
            AddressCity = city,
            AddressState = state,
            AddressPostalCode = postalCode,
            AddressCountry = "USA",
        };

        customer.Id = await customers.InsertAsync(connection, customer);
        return customer;
    }

    private async Task AddOrderAsync(
        DbConnection connection,
        string orderNumber,
        Customer customer,
        OrderStatus status,
        int daysAgo,
        string? notes,
        params (Product Product, int Quantity)[] lines)
    {
        var order = new Order
        {
            OrderNumber = orderNumber,
            CustomerId = customer.Id,
            Status = status,
            OrderedAt = clock.GetLocalNow().DateTime.AddDays(-daysAgo),
            Notes = notes,
        };

        order.Id = await orders.InsertAsync(connection, order);

        foreach (var (product, quantity) in lines)
        {
            // Unit price is the product's current price, which is what a real line
            // would snapshot at creation time.
            await orders.InsertLineAsync(connection, order.Id, new OrderItem
            {
                ProductId = product.Id,
                Quantity = quantity,
                UnitPrice = product.Price,
            });
        }
    }
}
