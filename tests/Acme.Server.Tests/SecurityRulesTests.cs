using System.Net;

namespace Acme.Server.Tests;

/// <summary>
/// End-to-end checks on the rules the client only makes invisible.
///
/// Ported from the Java <c>SecurityRulesTest</c>, and kept for the same reason its
/// javadoc gives: hiding a button and refusing the request behind it are separate
/// mechanisms, and only the second one is security. Every refusal here is asserted
/// against the API, with no client involved.
/// </summary>
public class SecurityRulesTests(AcmeApiFactory factory) : ApiTestBase(factory)
{
    private static object AProduct(string sku) =>
        new { sku, name = "Test", description = (string?)null, price = 1.00m, stockQuantity = 1, active = true };

    private static object AnOrder(long customerId, long productId) =>
        new { customerId, status = "NEW", notes = (string?)null, items = new[] { new { productId, quantity = 1 } } };

    [Fact]
    public async Task AnonymousRequestToAnAppScreen_isRefused()
    {
        var response = await Anonymous().GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousWrite_isRefused()
    {
        var response = await Anonymous().SendWithoutTokenAsync(HttpMethod.Delete, "/api/products/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_canReadEveryList()
    {
        var staff = await StaffAsync();

        foreach (var path in new[] { "/api/products", "/api/customers", "/api/orders", "/api/dashboard" })
        {
            var response = await staff.GetAsync(path);
            Assert.True(response.IsSuccessStatusCode, $"{path} returned {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task Staff_cannotCreateAProduct()
    {
        var staff = await StaffAsync();
        var response = await staff.PostAsync("/api/products", AProduct("BX-STAFF-CREATE"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_cannotUpdateACustomer()
    {
        var staff = await StaffAsync();
        var response = await staff.PutAsync(
            "/api/customers/1",
            new { firstName = "A", lastName = "B", email = "a.b@example.test", phone = (string?)null, address = (object?)null });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_cannotDeleteAnOrder()
    {
        var staff = await StaffAsync();
        var response = await staff.DeleteAsync("/api/orders/3");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Orders are the one area staff may change.</summary>
    [Fact]
    public async Task Staff_canCreateAnOrder()
    {
        var staff = await StaffAsync();
        var response = await staff.PostAsync("/api/orders", AnOrder(customerId: 1, productId: 1));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Admin_canCreateAProduct()
    {
        var admin = await AdminAsync();
        var response = await admin.PostAsync("/api/products", AProduct("BX-ADMIN-CREATE"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Before the antiforgery filter was added, every state-changing route here was a
    /// tokenless call. Being an administrator does not exempt you from it.
    /// </summary>
    [Fact]
    public async Task WriteWithoutAnAntiforgeryToken_isRefusedEvenForAnAdmin()
    {
        var admin = await AdminAsync();
        var response = await admin.SendWithoutTokenAsync(
            HttpMethod.Post, "/api/products", AProduct("BX-NOCSRF"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SignInWithABadPassword_isRefusedWithoutSayingWhichHalfWasWrong()
    {
        var session = Anonymous();

        var wrongPassword = await session.PostAsync("/api/auth/login", new { username = "adoyle", password = "nope" });
        var noSuchUser = await session.PostAsync("/api/auth/login", new { username = "nobody", password = "nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, noSuchUser.StatusCode);

        var first = (await ApiSession.JsonAsync(wrongPassword))["detail"]!.GetValue<string>();
        var second = (await ApiSession.JsonAsync(noSuchUser))["detail"]!.GetValue<string>();
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Me_describesTheSignedInUser()
    {
        var staff = await StaffAsync();
        var me = await staff.GetJsonAsync("/api/auth/me");

        Assert.Equal("sokonkwo", me["username"]!.GetValue<string>());
        Assert.Equal("Sam Okonkwo", me["displayName"]!.GetValue<string>());
        Assert.Equal("STAFF", me["roles"]!.AsArray()[0]!.GetValue<string>());
    }
}
