using System.Net;

namespace Acme.Server.Tests;

/// <summary>
/// Product and customer rules, ported from the Java <c>ProductServiceTest</c> and
/// <c>CustomerServiceTest</c>.
/// </summary>
public class CatalogApiTests(AcmeApiFactory factory) : ApiTestBase(factory)
{
    private static object Product(string sku, string name = "Test", decimal price = 1.00m, int stock = 1, bool active = true) =>
        new { sku, name, description = (string?)null, price, stockQuantity = stock, active };

    private static object Customer(string first, string last, string email) =>
        new { firstName = first, lastName = last, email, phone = (string?)null, address = (object?)null };

    // ---- uniqueness ---------------------------------------------------------

    /// <summary>
    /// The service compares case-insensitively while the database constraint is
    /// case-sensitive, so a differently-cased duplicate has to be caught here rather
    /// than surfacing as a 500 from the constraint.
    /// </summary>
    [Fact]
    public async Task DuplicateSku_isAFieldErrorEvenWhenTheCaseDiffers()
    {
        var admin = await AdminAsync();
        var response = await admin.PostAsync("/api/products", Product("bx-widget-01"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ApiSession.JsonAsync(response);
        Assert.Contains(
            "is already used by another product",
            problem["errors"]!["Sku"]!.AsArray()[0]!.GetValue<string>());
    }

    [Fact]
    public async Task DuplicateEmail_isAFieldErrorEvenWhenTheCaseDiffers()
    {
        var admin = await AdminAsync();
        var response = await admin.PostAsync(
            "/api/customers", Customer("Someone", "Else", "DANA.WHITFIELD@acme-supply.example"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ApiSession.JsonAsync(response);
        Assert.Contains(
            "is already used by another customer",
            problem["errors"]!["Email"]!.AsArray()[0]!.GetValue<string>());
    }

    [Fact]
    public async Task UpdatingAProduct_mayKeepItsOwnSku()
    {
        var admin = await AdminAsync();
        var created = await ApiSession.OkJsonAsync(await admin.PostAsync("/api/products", Product("BX-KEEP-SKU")));
        var id = created["id"]!.GetValue<long>();

        var response = await admin.PutAsync($"/api/products/{id}", Product("BX-KEEP-SKU", name: "Renamed"));

        var updated = await ApiSession.OkJsonAsync(response);
        Assert.Equal("Renamed", updated["name"]!.GetValue<string>());
    }

    // ---- guard ordering -----------------------------------------------------

    /// <summary>
    /// Editing a row that does not exist is a 404, reported before the uniqueness
    /// check -- otherwise a missing record would be reported as a duplicate.
    /// </summary>
    [Fact]
    public async Task UpdatingAMissingProduct_isNotFound_notADuplicate()
    {
        var admin = await AdminAsync();
        var response = await admin.PutAsync("/api/products/9999", Product("bx-widget-01"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletingAMissingCustomer_isNotFound_notAnInUseRefusal()
    {
        var admin = await AdminAsync();
        var response = await admin.DeleteAsync("/api/customers/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- referential guards -------------------------------------------------

    [Fact]
    public async Task DeletingAProductOnAnOrderLine_isRefusedWithAdvice()
    {
        var admin = await AdminAsync();
        var response = await admin.DeleteAsync("/api/products/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var detail = (await ApiSession.JsonAsync(response))["detail"]!.GetValue<string>();
        Assert.Contains("appears on", detail);
        Assert.Contains("Mark it inactive instead.", detail);
    }

    [Fact]
    public async Task DeletingACustomerWithOrders_isRefusedWithAdvice()
    {
        var admin = await AdminAsync();
        var response = await admin.DeleteAsync("/api/customers/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var detail = (await ApiSession.JsonAsync(response))["detail"]!.GetValue<string>();
        Assert.Contains("Delete or reassign those orders first.", detail);
    }

    [Fact]
    public async Task AProductOnNoOrderLine_canBeDeleted()
    {
        var admin = await AdminAsync();
        var created = await ApiSession.OkJsonAsync(await admin.PostAsync("/api/products", Product("BX-UNUSED")));
        var id = created["id"]!.GetValue<long>();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/products/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/products/{id}")).StatusCode);
    }

    // ---- validation ---------------------------------------------------------

    [Theory]
    [InlineData(-0.01, 1, "Price")]
    [InlineData(1.00, -1, "StockQuantity")]
    public async Task NegativeValues_areRefused(double price, int stock, string field)
    {
        var admin = await AdminAsync();
        var response = await admin.PostAsync("/api/products", Product("BX-NEG", price: (decimal)price, stock: stock));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ApiSession.JsonAsync(response);
        Assert.Contains("must not be negative", problem["errors"]![field]!.AsArray()[0]!.GetValue<string>());
    }

    /// <summary>
    /// A price with more than two decimals is refused rather than quietly rounded on
    /// the way into a decimal(19, 2) column.
    /// </summary>
    [Fact]
    public async Task PriceWithThreeDecimals_isRefused()
    {
        var admin = await AdminAsync();
        var response = await admin.PostAsync("/api/products", Product("BX-3DP", price: 1.005m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BadEmail_isRefused()
    {
        var admin = await AdminAsync();
        var response = await admin.PostAsync("/api/customers", Customer("A", "B", "not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- money fidelity -----------------------------------------------------

    /// <summary>
    /// SQLite has no exact decimal type, so this is worth asserting explicitly: a
    /// price must come back exactly as it went in, not as a float approximation.
    /// </summary>
    [Theory]
    [InlineData("0.01")]
    [InlineData("19.99")]
    [InlineData("34.50")]
    [InlineData("129.00")]
    [InlineData("99999.95")]
    public async Task PricesRoundTripExactly(string raw)
    {
        var admin = await AdminAsync();
        var price = decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);

        var created = await ApiSession.OkJsonAsync(
            await admin.PostAsync("/api/products", Product($"BX-MONEY-{raw}", price: price)));
        var id = created["id"]!.GetValue<long>();

        var read = await admin.GetJsonAsync($"/api/products/{id}");
        Assert.Equal(price, read["price"]!.GetValue<decimal>());
    }

    // ---- customer detail ----------------------------------------------------

    [Fact]
    public async Task CustomerRead_carriesItsOrderCountAndAddress()
    {
        var admin = await AdminAsync();
        var customer = await admin.GetJsonAsync("/api/customers/1");

        Assert.Equal("Dana Whitfield", customer["fullName"]!.GetValue<string>());
        Assert.True(customer["hasAddress"]!.GetValue<bool>());
        Assert.Equal("Chicago", customer["address"]!["city"]!.GetValue<string>());
        Assert.True(customer["orderCount"]!.GetValue<long>() >= 1);
    }
}
