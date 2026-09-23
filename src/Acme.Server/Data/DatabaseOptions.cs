namespace Acme.Server.Data;

/// <summary>
/// Which engine to talk to and how to reach it. Bound from the <c>Database</c> section
/// of configuration, so a deployment can switch engines without a rebuild -- the same
/// property Acme got from <c>DB_URL</c> plus a Maven profile, minus the build step.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public DbProvider Provider { get; set; } = DbProvider.Sqlite;

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Run outstanding migrations on startup. On by default in Development.</summary>
    public bool MigrateOnStartup { get; set; }
}

public enum DbProvider
{
    Sqlite,
    Postgres,
    MySql,
    SqlServer,
}
