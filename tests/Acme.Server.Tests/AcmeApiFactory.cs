using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Acme.Server.Tests;

/// <summary>
/// Boots the real application over a throwaway SQLite file, with the migrations
/// applied and the demo data seeded.
///
/// These are integration tests rather than mock-based unit tests, and that is a
/// deliberate choice for this port: every query is hand-written SQL, so a test that
/// substitutes the repository proves the service's arithmetic and nothing about the
/// statement that actually runs. Two of the bugs found while building this -- Insight
/// binding decimals as empty strings, and its dictionary parameter path throwing on
/// the second call -- were invisible to anything that did not touch a database.
/// </summary>
public sealed class AcmeApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"acme-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:ConnectionString", $"Data Source={_databasePath}");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:Enabled", "true");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing) return;

        try
        {
            if (File.Exists(_databasePath)) File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // A temp file the OS still has open is not worth failing a test run over.
        }
    }
}

/// <summary>
/// One application per test class. Each class gets its own database, so tests that
/// write cannot tread on each other across classes.
/// </summary>
public abstract class ApiTestBase : IClassFixture<AcmeApiFactory>
{
    protected ApiTestBase(AcmeApiFactory factory) => Factory = factory;

    protected AcmeApiFactory Factory { get; }

    protected Task<ApiSession> AdminAsync() => ApiSession.SignInAsync(Factory, "adoyle", "admin");

    protected Task<ApiSession> StaffAsync() => ApiSession.SignInAsync(Factory, "sokonkwo", "staff");

    protected ApiSession Anonymous() => ApiSession.Anonymous(Factory);
}
