using System.Data.Common;
using DbUp;
using DbUp.Builder;
using Insight.Database.Providers.MySqlConnector;
using MySqlConnector;

namespace Acme.Server.Data.Dialect;

/// <summary>
/// MySQL, over the MySqlConnector driver rather than Oracle's MySql.Data.
///
/// The driver choice is not incidental: MySql.Data drags in System.Drawing.Common 4.7.0
/// and BouncyCastle 2.2.1, both of which carry known advisories. MySqlConnector has the
/// same surface, is what Insight's MySqlConnector provider targets, and brings neither.
/// </summary>
public sealed class MySqlDialect : ISqlDialect
{
    static MySqlDialect() => MySqlConnectorInsightDbProvider.RegisterProvider();

    public DbProvider Provider => DbProvider.MySql;

    public string MigrationFolder => "mysql";

    public bool SupportsDecimalAggregate => true;

    public DbConnection CreateConnection(string connectionString) => new MySqlConnection(connectionString);

    public string Paginate(string sql, int offset, int limit) => $"{sql}\nLIMIT {limit} OFFSET {offset}";

    public string InsertReturningId(string insertSql) => $"{insertSql};\nSELECT LAST_INSERT_ID();";

    public UpgradeEngineBuilder CreateUpgradeEngine(string connectionString) =>
        DeployChanges.To.MySqlDatabase(connectionString);
}
