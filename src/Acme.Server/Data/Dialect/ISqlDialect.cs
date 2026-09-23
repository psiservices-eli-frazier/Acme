using System.Data.Common;
using DbUp.Builder;

namespace Acme.Server.Data.Dialect;

/// <summary>
/// The one place in the application that knows which database engine is in use.
///
/// Acme guarded this carefully -- "no Java code and no mapping annotation names a
/// specific database" -- by leaning on Hibernate to infer a dialect from the live
/// connection. Insight.Database is SQL-first, so there is nothing to infer from and the
/// differences have to be named somewhere. They are named here, and nowhere else:
/// repositories, services and endpoints must stay engine-agnostic.
///
/// If you find yourself writing <c>if (provider == DbProvider.X)</c> outside this
/// namespace, add a member to this interface instead.
/// </summary>
public interface ISqlDialect
{
    DbProvider Provider { get; }

    /// <summary>
    /// Folder under <c>Data/Migrations</c> holding this engine's DbUp scripts.
    /// </summary>
    string MigrationFolder { get; }

    /// <summary>
    /// True when the engine can <c>SUM</c> money exactly in SQL. False forces callers
    /// onto a fetch-and-sum-in-C# path -- see <c>OrderRepository.GetTotalsAsync</c>.
    /// SQLite has no exact decimal type, so it is the one engine that may answer false.
    /// </summary>
    bool SupportsDecimalAggregate { get; }

    DbConnection CreateConnection(string connectionString);

    /// <summary>
    /// Appends this engine's paging clause. Every caller supplies a query that already
    /// has an ORDER BY, which SQL Server's OFFSET/FETCH form requires.
    /// </summary>
    string Paginate(string sql, int offset, int limit);

    /// <summary>
    /// Turns an INSERT into a statement that yields the generated identity as a single
    /// scalar, for execution with <c>ExecuteScalarSqlAsync&lt;long&gt;</c>.
    /// </summary>
    string InsertReturningId(string insertSql);

    /// <summary>
    /// Starts a DbUp upgrade engine for this engine. The caller adds the scripts and
    /// builds; only the choice of provider-specific builder belongs here.
    /// </summary>
    UpgradeEngineBuilder CreateUpgradeEngine(string connectionString);
}
