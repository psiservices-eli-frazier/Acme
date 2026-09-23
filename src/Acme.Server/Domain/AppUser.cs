namespace Acme.Server.Domain;

/// <summary>
/// A sign-in account. Maps to <c>app_users</c>, with roles in the <c>app_user_roles</c>
/// side table.
///
/// The table is named <c>app_users</c> rather than <c>users</c> because <c>user</c> is a
/// reserved word in PostgreSQL.
///
/// Usernames are stored lower-cased on write and looked up case-insensitively, so the
/// case-sensitive unique constraint and the login lookup cannot disagree about what
/// counts as a duplicate.
/// </summary>
public class AppUser
{
    public long Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Always loaded in the same round trip as the user. Authorization runs on every
    /// request from the cookie principal, long after the lookup connection has closed,
    /// so there is nothing to lazy-load from.
    /// </summary>
    public List<Role> Roles { get; set; } = [];
}
