using Acme.Server.Data;
using Acme.Server.Data.Repositories;
using Acme.Server.Domain;
using Acme.Server.Security;

namespace Acme.Server.Seed;

/// <summary>
/// Creates the throwaway demo accounts, for local use only.
///
/// Accounts exist so role checks have something to resolve against: there is no screen,
/// endpoint or service for managing them. Provisioning a first account outside
/// development is handled by the <c>--create-admin</c> startup command instead -- the
/// gap BrandX flagged as unsolved.
/// </summary>
public sealed class DevUserSeeder(
    IDbConnectionFactory connections,
    IAppUserRepository users,
    SignInService auth,
    ILogger<DevUserSeeder> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        await using var connection = await connections.OpenWithTransactionAsync(ct);

        if (await users.CountAsync(connection) > 0)
        {
            return;
        }

        await InsertAsync(connection, "adoyle", "admin", "Avery Doyle", Role.Admin);
        await InsertAsync(connection, "sokonkwo", "staff", "Sam Okonkwo", Role.Staff);
        await InsertAsync(connection, "jdoe", "staff", "Jane Doe", Role.Staff);

        connection.Commit();

        logger.LogWarning(
            "Seeded demo accounts for local use: adoyle/admin (ADMIN), sokonkwo/staff and jdoe/staff (STAFF).");
    }

    private Task InsertAsync(
        System.Data.Common.DbConnection connection,
        string username,
        string password,
        string displayName,
        params Role[] roles) =>
        users.InsertAsync(connection, new AppUser
        {
            Username = username,
            PasswordHash = auth.Hash(password),
            DisplayName = displayName,
            Enabled = true,
            Roles = [.. roles],
        });
}
