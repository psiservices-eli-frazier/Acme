using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Acme.Server.Data;

/// <summary>
/// Confirms the configured database is reachable by opening a connection through the
/// same <see cref="IDbConnectionFactory"/> every repository uses, then running a trivial
/// query -- so this fails exactly when a real request would, regardless of which of the
/// four dialects is configured.
/// </summary>
public sealed class DatabaseHealthCheck(IDbConnectionFactory connectionFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("Database connection succeeded.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed.", ex);
        }
    }
}
