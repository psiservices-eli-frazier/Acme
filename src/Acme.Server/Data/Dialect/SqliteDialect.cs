using System.Data.Common;
using System.Data.SQLite;
using DbUp;
using DbUp.Builder;

namespace Acme.Server.Data.Dialect;

/// <summary>
/// SQLite -- the zero-install dev and demo default, standing in for the H2 in-memory
/// database Acme used. H2 is a Java database with no ADO.NET driver, so it could not
/// come along; SQLite keeps the property H2 was chosen for, that the app runs with
/// nothing installed.
///
/// The driver is <c>System.Data.SQLite</c>, which is the driver Insight.Database
/// documents as tested. That choice is load-bearing, not incidental: with
/// <c>Microsoft.Data.Sqlite</c>, Insight binds every <c>decimal</c> and
/// <c>DateTime</c> parameter as an empty string -- prices and timestamps silently
/// persist as '' and the next read fails on the way back out. Integers, strings and
/// booleans are unaffected, so the failure does not show up until a money or date
/// column is read. Do not swap the driver back without re-running that check.
///
/// DbUp keeps its own SQLite driver (Microsoft.Data.Sqlite, via dbup-sqlite). Both are
/// referenced deliberately: migrations and queries never share a connection.
/// </summary>
public sealed class SqliteDialect : ISqlDialect
{
    public DbProvider Provider => DbProvider.Sqlite;

    public string MigrationFolder => "sqlite";

    /// <summary>
    /// SQLite has no exact decimal type. Summing money in SQL would coerce the stored
    /// values to doubles and accumulate float error, so order totals are read as
    /// decimals and summed in C# on this engine instead.
    /// </summary>
    public bool SupportsDecimalAggregate => false;

    public DbConnection CreateConnection(string connectionString) => new SQLiteConnection(connectionString);

    public string Paginate(string sql, int offset, int limit) => $"{sql}\nLIMIT {limit} OFFSET {offset}";

    public string InsertReturningId(string insertSql) => $"{insertSql};\nSELECT last_insert_rowid();";

    public UpgradeEngineBuilder CreateUpgradeEngine(string connectionString) =>
        DeployChanges.To.SqliteDatabase(connectionString);
}
