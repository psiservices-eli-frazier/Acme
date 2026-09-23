using System.Data.Common;
using Acme.Server.Data.Dialect;
using Acme.Server.Domain;
using Insight.Database;

namespace Acme.Server.Data.Repositories;

public interface IAppUserRepository
{
    /// <summary>
    /// Case-insensitive lookup, always with the user's roles. Authorization runs on
    /// every request from the cookie principal, long after this connection has closed,
    /// so there is nothing to load the roles from later -- the Java version attached an
    /// <c>@EntityGraph</c> here for exactly the same reason.
    /// </summary>
    Task<AppUser?> FindByUsernameAsync(DbConnection connection, string username);

    Task<bool> ExistsByUsernameAsync(DbConnection connection, string username);

    Task<long> CountAsync(DbConnection connection);

    Task<long> InsertAsync(DbConnection connection, AppUser user);
}

public sealed class AppUserRepository(ISqlDialect dialect, TimeProvider clock) : IAppUserRepository
{
    private const string Columns = """
        u.id            AS Id,
        u.username      AS Username,
        u.password_hash AS PasswordHash,
        u.display_name  AS DisplayName,
        u.enabled       AS Enabled,
        u.created_at    AS CreatedAt
        """;

    public async Task<AppUser?> FindByUsernameAsync(DbConnection connection, string username)
    {
        var user = await connection.SingleSqlAsync<AppUser>(
            $"SELECT {Columns} FROM app_users u WHERE lower(u.username) = lower(@Username)",
            new { Username = username });

        if (user is null)
        {
            return null;
        }

        var roles = await connection.QuerySqlAsync<string>(
            "SELECT r.role FROM app_user_roles r WHERE r.user_id = @UserId",
            new { UserId = user.Id });

        user.Roles = [.. roles.Select(RoleExtensions.FromDbValue)];
        return user;
    }

    public async Task<bool> ExistsByUsernameAsync(DbConnection connection, string username)
    {
        var count = await connection.ExecuteScalarSqlAsync<long>(
            "SELECT count(*) FROM app_users u WHERE lower(u.username) = lower(@Username)",
            new { Username = username });

        return count > 0;
    }

    public Task<long> CountAsync(DbConnection connection) =>
        connection.ExecuteScalarSqlAsync<long>("SELECT count(*) FROM app_users u", new { });

    public async Task<long> InsertAsync(DbConnection connection, AppUser user)
    {
        user.CreatedAt = clock.GetLocalNow().DateTime;

        var sql = dialect.InsertReturningId(
            """
            INSERT INTO app_users (username, password_hash, display_name, enabled, created_at)
            VALUES (@Username, @PasswordHash, @DisplayName, @Enabled, @CreatedAt)
            """);

        var id = await connection.ExecuteScalarSqlAsync<long>(
            sql,
            new
            {
                // Lower-cased on write so the case-sensitive unique constraint and the
                // case-insensitive lookup cannot disagree about what is a duplicate.
                Username = user.Username.ToLowerInvariant(),
                user.PasswordHash,
                user.DisplayName,
                user.Enabled,
                user.CreatedAt,
            });

        foreach (var role in user.Roles.Distinct())
        {
            await connection.ExecuteSqlAsync(
                "INSERT INTO app_user_roles (user_id, role) VALUES (@UserId, @Role)",
                new { UserId = id, Role = role.ToDbValue() });
        }

        user.Id = id;
        return id;
    }
}
