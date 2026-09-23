using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Acme.Server.Tests;

/// <summary>
/// The order rules, ported from the Java <c>OrderServiceTest</c>.
/// </summary>
public class OrderApiTests(AcmeApiFactory factory) : ApiTestBase(factory)
{
    private static object Order(long customerId, string status, params (long ProductId, int Quantity)[] lines) =>
        new
        {
            customerId,
            status,
            notes = (string?)null,
            items = lines.Select(l => new { productId = l.ProductId, quantity = l.Quantity }).ToArray(),
        };

    private static async Task<long> CreateProductAsync(ApiSession admin, string sku, decimal price)
    {
        var response = await admin.PostAsync(
            "/api/products",
            new { sku, name = sku, description = (string?)null, price, stockQuantity = 100, active = true });

        return (await ApiSession.OkJsonAsync(response))["id"]!.GetValue<long>();
    }

    private static JsonNode LineFor(JsonNode order, long productId) =>
        order["items"]!.AsArray().First(line => line!["productId"]!.GetValue<long>() == productId)!;

    private static decimal UnitPriceOf(JsonNode order, long productId) =>
        LineFor(order, productId)["unitPrice"]!.GetValue<decimal>();

    [Fact]
    public async Task CreatedOrder_getsAGeneratedOrderNumber()
    {
        var admin = await AdminAsync();
        var created = await ApiSession.OkJsonAsync(
            await admin.PostAsync("/api/orders", Order(1, "NEW", (1, 2))));

        Assert.Matches(new Regex(@"^BX-\d{8}-\d{4}$"), created["orderNumber"]!.GetValue<string>());
    }

    /// <summary>
    /// The rule the order screen's hint text describes, and the one most worth
    /// getting right: a product already booked on the order keeps the price it was
    /// booked at, while a product added now takes today's price.
    /// </summary>
    [Fact]
    public async Task Update_reusedProductKeepsItsBookedPrice_newProductTakesTodaysPrice()
    {
        var admin = await AdminAsync();

        var booked = await CreateProductAsync(admin, "BX-SNAP-BOOKED", 10.00m);
        var added = await CreateProductAsync(admin, "BX-SNAP-ADDED", 50.00m);

        var order = await ApiSession.OkJsonAsync(
            await admin.PostAsync("/api/orders", Order(1, "NEW", (booked, 1))));
        var orderId = order["id"]!.GetValue<long>();

        Assert.Equal(10.00m, UnitPriceOf(order, booked));

        // Reprice the already-booked product well away from where it was booked.
        await admin.PutAsync(
            $"/api/products/{booked}",
            new { sku = "BX-SNAP-BOOKED", name = "BX-SNAP-BOOKED", description = (string?)null, price = 99.00m, stockQuantity = 100, active = true });

        var updated = await ApiSession.OkJsonAsync(
            await admin.PutAsync($"/api/orders/{orderId}", Order(1, "NEW", (booked, 3), (added, 1))));

        Assert.Equal(10.00m, UnitPriceOf(updated, booked));
        Assert.Equal(50.00m, UnitPriceOf(updated, added));
        Assert.Equal(3, LineFor(updated, booked)["quantity"]!.GetValue<int>());
    }

    /// <summary>
    /// A product removed from the order and re-added in the <em>same</em> submit still
    /// counts as booked, because the snapshot is taken from what is persisted before
    /// the lines are replaced.
    /// </summary>
    [Fact]
    public async Task Update_productRemovedAndReaddedInTheSameSubmit_keepsItsBookedPrice()
    {
        var admin = await AdminAsync();
        var product = await CreateProductAsync(admin, "BX-SNAP-READD", 20.00m);

        var order = await ApiSession.OkJsonAsync(
            await admin.PostAsync("/api/orders", Order(2, "NEW", (product, 1))));
        var orderId = order["id"]!.GetValue<long>();

        await admin.PutAsync(
            $"/api/products/{product}",
            new { sku = "BX-SNAP-READD", name = "BX-SNAP-READD", description = (string?)null, price = 77.00m, stockQuantity = 100, active = true });

        var updated = await ApiSession.OkJsonAsync(
            await admin.PutAsync($"/api/orders/{orderId}", Order(2, "NEW", (product, 9))));

        Assert.Equal(20.00m, UnitPriceOf(updated, product));
    }

    [Fact]
    public async Task Update_leavesTheOrderNumberAndOrderedAtAlone()
    {
        var admin = await AdminAsync();
        var created = await ApiSession.OkJsonAsync(
            await admin.PostAsync("/api/orders", Order(1, "NEW", (1, 1))));

        var orderId = created["id"]!.GetValue<long>();
        var number = created["orderNumber"]!.GetValue<string>();
        var orderedAt = created["orderedAt"]!.GetValue<string>();

        var updated = await ApiSession.OkJsonAsync(
            await admin.PutAsync($"/api/orders/{orderId}", Order(3, "SHIPPED", (2, 4))));

        Assert.Equal(number, updated["orderNumber"]!.GetValue<string>());
        Assert.Equal(orderedAt, updated["orderedAt"]!.GetValue<string>());
        Assert.Equal("SHIPPED", updated["status"]!.GetValue<string>());
        Assert.Equal(3, updated["customer"]!["id"]!.GetValue<long>());
    }

    [Fact]
    public async Task OrderTotal_isTheSumOfItsLines_onBothTheDetailAndTheList()
    {
        var admin = await AdminAsync();
        var a = await CreateProductAsync(admin, "BX-TOTAL-A", 19.99m);
        var b = await CreateProductAsync(admin, "BX-TOTAL-B", 7.25m);

        var created = await ApiSession.OkJsonAsync(
            await admin.PostAsync("/api/orders", Order(1, "NEW", (a, 40), (b, 120))));
        var orderId = created["id"]!.GetValue<long>();

        // 40 * 19.99 + 120 * 7.25
        Assert.Equal(1669.60m, created["total"]!.GetValue<decimal>());

        // The list total comes from a separate aggregate, so it is worth asserting
        // that it agrees with the one computed from the loaded lines.
        var list = await admin.GetJsonAsync($"/api/orders?customerId=1&size=100");
        var row = list["content"]!.AsArray().First(o => o!["id"]!.GetValue<long>() == orderId)!;
        Assert.Equal(1669.60m, row["total"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task OrderWithNoLines_isRefused()
    {
        var admin = await AdminAsync();
        var response = await admin.PostAsync("/api/orders", Order(1, "NEW"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ApiSession.JsonAsync(response);
        Assert.Contains("at least one line item", problem["errors"]!["Items"]!.AsArray()[0]!.GetValue<string>());
    }

    [Fact]
    public async Task LineWithNoProduct_isRefused()
    {
        var admin = await AdminAsync();
        var response = await admin.PostAsync(
            "/api/orders",
            new { customerId = 1, status = "NEW", notes = (string?)null, items = new[] { new { productId = (long?)null, quantity = 1 } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LineWithZeroQuantity_isRefused()
    {
        var admin = await AdminAsync();
        var response = await admin.PostAsync("/api/orders", Order(1, "NEW", (1, 0)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ApiSession.JsonAsync(response);
        Assert.Contains(
            "Quantity must be at least 1",
            problem["errors"]!.AsObject().First().Value!.AsArray()[0]!.GetValue<string>());
    }

    [Fact]
    public async Task OrderForAMissingCustomer_isNotFound()
    {
        var admin = await AdminAsync();
        var response = await admin.PostAsync("/api/orders", Order(9999, "NEW", (1, 1)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Nothing references an order, so there is no in-use guard on deleting one.</summary>
    [Fact]
    public async Task DeletingAnOrder_takesItsLinesWithIt()
    {
        var admin = await AdminAsync();
        var created = await ApiSession.OkJsonAsync(
            await admin.PostAsync("/api/orders", Order(2, "NEW", (1, 1), (2, 2))));
        var orderId = created["id"]!.GetValue<long>();

        var deleted = await admin.DeleteAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var gone = await admin.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }
}
