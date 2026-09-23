using System.Net;

namespace Acme.Server.Tests;

/// <summary>
/// The list screens' query-string contract: search, paging and sorting.
///
/// The contract is deliberately identical to the one Spring Data produced, because
/// the client rebuilds its links from it -- a sort toggle compares the echoed value
/// against a literal.
/// </summary>
public class ListApiTests(AcmeApiFactory factory) : ApiTestBase(factory)
{
    [Theory]
    [InlineData("/api/products", "name,asc")]
    [InlineData("/api/customers", "lastName,asc")]
    [InlineData("/api/orders", "orderedAt,desc")]
    public async Task DefaultSort_matchesTheJavaPageableDefaults(string path, string expected)
    {
        var admin = await AdminAsync();
        var page = await admin.GetJsonAsync(path);

        Assert.Equal(expected, page["sort"]!.GetValue<string>());
        Assert.Equal(0, page["page"]!.GetValue<int>());
        Assert.Equal(10, page["size"]!.GetValue<int>());
    }

    /// <summary>
    /// The canonical spelling comes back, not whatever casing was asked for -- the
    /// client's sort toggle compares against a literal and would stick otherwise.
    /// </summary>
    [Fact]
    public async Task RequestedSort_isEchoedInItsCanonicalSpelling()
    {
        var admin = await AdminAsync();
        var page = await admin.GetJsonAsync("/api/products?sort=STOCKQUANTITY,desc");

        Assert.Equal("stockQuantity,desc", page["sort"]!.GetValue<string>());
    }

    [Fact]
    public async Task SortingByPrice_ordersNumerically_notLexicographically()
    {
        var admin = await AdminAsync();
        var page = await admin.GetJsonAsync("/api/products?sort=price,asc&size=100");

        var prices = page["content"]!.AsArray().Select(p => p!["price"]!.GetValue<decimal>()).ToList();
        Assert.Equal(prices.OrderBy(p => p).ToList(), prices);
    }

    [Theory]
    [InlineData("/api/products", "bogus")]
    [InlineData("/api/products", "description")]
    [InlineData("/api/orders", "customer.email")]
    [InlineData("/api/customers", "firstName")]
    public async Task SortOutsideTheAllowList_isRefused(string path, string sort)
    {
        var admin = await AdminAsync();
        var response = await admin.GetAsync($"{path}?sort={sort}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ApiSession.JsonAsync(response);
        Assert.Contains("is not a sortable column", problem["errors"]!["sort"]!.AsArray()[0]!.GetValue<string>());
    }

    [Fact]
    public async Task Paging_walksTheWholeSetWithoutRepeatingOrSkipping()
    {
        var admin = await AdminAsync();

        var all = await admin.GetJsonAsync("/api/products?size=100&sort=sku,asc");
        var total = all["totalElements"]!.GetValue<int>();
        var expected = all["content"]!.AsArray().Select(p => p!["sku"]!.GetValue<string>()).ToList();

        var seen = new List<string>();
        for (var page = 0; ; page++)
        {
            var body = await admin.GetJsonAsync($"/api/products?size=2&page={page}&sort=sku,asc");
            seen.AddRange(body["content"]!.AsArray().Select(p => p!["sku"]!.GetValue<string>()));
            if (body["last"]!.GetValue<bool>()) break;
        }

        Assert.Equal(total, seen.Count);
        Assert.Equal(expected, seen);
    }

    [Fact]
    public async Task Search_isCaseInsensitiveAndEchoedBack()
    {
        var admin = await AdminAsync();
        var page = await admin.GetJsonAsync("/api/products?search=WIDGET");

        Assert.Equal("WIDGET", page["search"]!.GetValue<string>());
        Assert.True(page["totalElements"]!.GetValue<int>() >= 2);
    }

    [Fact]
    public async Task BlankSearch_returnsEverything()
    {
        var admin = await AdminAsync();

        var unfiltered = await admin.GetJsonAsync("/api/products");
        var blank = await admin.GetJsonAsync("/api/products?search=");

        Assert.Equal(
            unfiltered["totalElements"]!.GetValue<int>(),
            blank["totalElements"]!.GetValue<int>());
    }

    [Fact]
    public async Task OrderSearch_matchesTheCustomerAsWellAsTheOrderNumber()
    {
        var admin = await AdminAsync();

        var byNumber = await admin.GetJsonAsync("/api/orders?search=BX-SEED-0001");
        var byCustomer = await admin.GetJsonAsync("/api/orders?search=whitfield");

        Assert.Equal(1, byNumber["totalElements"]!.GetValue<int>());
        Assert.True(byCustomer["totalElements"]!.GetValue<int>() >= 1);
    }

    /// <summary>
    /// A real filter, unlike the Java screen, which linked a customer's orders by
    /// pasting their email into the search box.
    /// </summary>
    [Fact]
    public async Task CustomerFilter_narrowsToThatCustomerAndCombinesWithSearch()
    {
        var admin = await AdminAsync();

        var filtered = await admin.GetJsonAsync("/api/orders?customerId=1&size=100");
        Assert.All(
            filtered["content"]!.AsArray(),
            order => Assert.Equal(1, order!["customer"]!["id"]!.GetValue<long>()));

        var combined = await admin.GetJsonAsync("/api/orders?customerId=1&search=BX-SEED-0001");
        Assert.Equal(1, combined["totalElements"]!.GetValue<int>());

        var contradictory = await admin.GetJsonAsync("/api/orders?customerId=2&search=BX-SEED-0001");
        Assert.Equal(0, contradictory["totalElements"]!.GetValue<int>());
    }

    /// <summary>
    /// Repeating a query used to fail: Insight caches a parameter-generator delegate
    /// per SQL string on its dictionary path, and the cached delegate held a disposed
    /// command. Typed parameter objects avoid it, and this keeps it avoided.
    /// </summary>
    [Fact]
    public async Task TheSameFilteredQuery_canBeRunRepeatedly()
    {
        var admin = await AdminAsync();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.True((await admin.GetAsync("/api/orders?customerId=1")).IsSuccessStatusCode);
            Assert.True((await admin.GetAsync("/api/orders?search=seed")).IsSuccessStatusCode);
            Assert.True((await admin.GetAsync("/api/products?search=widget")).IsSuccessStatusCode);
            Assert.True((await admin.GetAsync("/api/customers?search=a")).IsSuccessStatusCode);
        }
    }

    [Fact]
    public async Task Dashboard_countsMatchTheListTotals()
    {
        var admin = await AdminAsync();
        var dashboard = await admin.GetJsonAsync("/api/dashboard");

        foreach (var (path, key) in new[]
                 {
                     ("/api/products", "productCount"),
                     ("/api/customers", "customerCount"),
                     ("/api/orders", "orderCount"),
                 })
        {
            var list = await admin.GetJsonAsync(path);
            Assert.Equal(list["totalElements"]!.GetValue<int>(), dashboard[key]!.GetValue<int>());
        }
    }
}
