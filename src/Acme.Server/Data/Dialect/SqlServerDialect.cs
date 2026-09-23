using System.Data.Common;
using DbUp;
using DbUp.Builder;
using Microsoft.Data.SqlClient;

namespace Acme.Server.Data.Dialect;

/// <summary>
/// SQL Server. Insight.Database has built-in support, so there is no provider to
/// register here.
/// </summary>
public sealed class SqlServerDialect : ISqlDialect
{
    public DbProvider Provider => DbProvider.SqlServer;

    public string MigrationFolder => "sqlserver";

    public bool SupportsDecimalAggregate => true;

    public DbConnection CreateConnection(string connectionString) => new SqlConnection(connectionString);

    /// <summary>
    /// OFFSET/FETCH requires an ORDER BY, which every paged query here supplies.
    /// </summary>
    public string Paginate(string sql, int offset, int limit) =>
        $"{sql}\nOFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY";

    public string InsertReturningId(string insertSql) =>
        $"{insertSql};\nSELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

    public UpgradeEngineBuilder CreateUpgradeEngine(string connectionString) =>
        DeployChanges.To.SqlDatabase(connectionString);
}
