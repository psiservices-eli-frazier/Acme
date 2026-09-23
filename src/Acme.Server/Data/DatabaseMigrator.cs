using Acme.Server.Data.Dialect;
using DbUp;
using Microsoft.Extensions.Options;

namespace Acme.Server.Data;

/// <summary>
/// Applies the outstanding DbUp scripts for the configured engine.
///
/// BrandX had no migration story at all -- Hibernate's <c>ddl-auto</c> generated the
/// schema in dev and the production profiles were set to <c>validate</c> with a comment
/// saying it was time to introduce Flyway. This is that missing piece.
/// </summary>
public sealed class DatabaseMigrator(
    ISqlDialect dialect,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseMigrator> logger)
{
    public void Run()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;

        // Scripts are embedded as Acme.Server.Data.Migrations.<dialect>.<name>.sql, so
        // the dialect folder selects the script set and the numeric prefixes order it.
        var prefix = $"Acme.Server.Data.Migrations.{dialect.MigrationFolder}.";

        var upgrader = dialect.CreateUpgradeEngine(options.Value.ConnectionString)
            .WithScriptsEmbeddedInAssembly(assembly, name => name.StartsWith(prefix, StringComparison.Ordinal))
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        if (!upgrader.IsUpgradeRequired())
        {
            logger.LogInformation("Database schema is up to date ({Provider}).", dialect.Provider);
            return;
        }

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Database migration failed on script '{result.ErrorScript?.Name}'.", result.Error);
        }

        logger.LogInformation(
            "Applied {Count} migration script(s) ({Provider}).", result.Scripts.Count(), dialect.Provider);
    }
}
