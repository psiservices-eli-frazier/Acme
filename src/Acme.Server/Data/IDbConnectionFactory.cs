using System.Data.Common;
using Acme.Server.Data.Dialect;
using Insight.Database;
using Microsoft.Extensions.Options;

namespace Acme.Server.Data;

/// <summary>
/// Hands out connections. Every read opens one and disposes it; every write opens one
/// with a transaction and commits explicitly.
///
/// Connections are passed to repository methods rather than held by them, so the
/// transaction boundary is visible in the service method that owns it -- the job
/// <c>@Transactional</c> did in the Java code.
/// </summary>
public interface IDbConnectionFactory
{
    ISqlDialect Dialect { get; }

    Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a connection with an open transaction. The caller must call
    /// <c>Commit()</c>; disposing without committing rolls back.
    /// </summary>
    Task<DbConnectionWrapper> OpenWithTransactionAsync(CancellationToken cancellationToken = default);
}

public sealed class DbConnectionFactory(ISqlDialect dialect, IOptions<DatabaseOptions> options)
    : IDbConnectionFactory
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public ISqlDialect Dialect { get; } = dialect;

    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = Dialect.CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public async Task<DbConnectionWrapper> OpenWithTransactionAsync(CancellationToken cancellationToken = default)
    {
        var connection = Dialect.CreateConnection(_connectionString);
        return await connection.OpenWithTransactionAsync();
    }
}
