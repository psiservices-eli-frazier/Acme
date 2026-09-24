using System.Net;

namespace Acme.Server.Tests;

/// <summary>
/// The health check is the one endpoint deliberately left open to anonymous callers --
/// see the note on <c>HealthEndpoints</c> for why -- so it is asserted here rather than
/// alongside the refusals in <see cref="SecurityRulesTests"/>.
/// </summary>
public class HealthApiTests(AcmeApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task AnonymousRequest_reportsHealthyDatabase()
    {
        var response = await Anonymous().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ApiSession.OkJsonAsync(response);
        Assert.Equal("Healthy", body["status"]!.GetValue<string>());

        var databaseCheck = body["checks"]![0]!;
        Assert.Equal("database", databaseCheck["name"]!.GetValue<string>());
        Assert.Equal("Healthy", databaseCheck["status"]!.GetValue<string>());
    }
}
