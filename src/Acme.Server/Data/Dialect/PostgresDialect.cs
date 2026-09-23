using System.Data.Common;
using DbUp;
using DbUp.Builder;
using Insight.Database.Providers.PostgreSQL;
using Npgsql;

namespace Acme.Server.Data.Dialect;

/// <summary>PostgreSQL.</summary>
public sealed class PostgresDialect : ISqlDialect
{
    static PostgresDialect() => PostgreSQLInsightDbProvider.RegisterProvider();

    public DbProvider Provider => DbProvider.Postgres;

    public string MigrationFolder => "postgres";

    public bool SupportsDecimalAggregate => true;

    public DbConnection CreateConnection(string connectionString) => new NpgsqlConnection(connectionString);

    public string Paginate(string sql, int offset, int limit) => $"{sql}\nLIMIT {limit} OFFSET {offset}";

    public string InsertReturningId(string insertSql) => $"{insertSql}\nRETURNING id";

    public UpgradeEngineBuilder CreateUpgradeEngine(string connectionString) =>
        DeployChanges.To.PostgresqlDatabase(connectionString);
}
