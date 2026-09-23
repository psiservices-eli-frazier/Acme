using System.Security.Claims;
using Acme.Server.Data;
using Acme.Server.Data.Repositories;
using Acme.Server.Domain;
using Microsoft.AspNetCore.Identity;

namespace Acme.Server.Security;

/// <summary>
/// Verifies credentials and builds the cookie principal.
///
/// Hashing uses ASP.NET Core's <see cref="PasswordHasher{TUser}"/>. Its format carries
/// its own version marker, which gives the property Acme got from Spring's
/// <c>{bcrypt}</c>-prefixed delegating encoder: changing algorithm later is a data
/// migration, not a forced password reset for everyone.
/// </summary>
public sealed class SignInService(
    IDbConnectionFactory connections,
    IAppUserRepository users,
    IPasswordHasher<AppUser> hasher)
{
    public const string DisplayNameClaim = "display_name";

    public string Hash(string password) => hasher.HashPassword(new AppUser(), password);

    /// <summary>
    /// Returns the principal for valid credentials, or null. The caller must not tell
    /// the user which half was wrong: distinguishing "no such account" from "wrong
    /// password" turns the sign-in form into a way to test whether an account exists.
    /// </summary>
    public async Task<ClaimsPrincipal?> AuthenticateAsync(
        string username,
        string password,
        string authenticationScheme,
        CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        var user = await users.FindByUsernameAsync(connection, username);

        if (user is null || !user.Enabled)
        {
            return null;
        }

        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return BuildPrincipal(user, authenticationScheme);
    }

    public static ClaimsPrincipal BuildPrincipal(AppUser user, string authenticationScheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(DisplayNameClaim, user.DisplayName),
        };

        // Roles are plain claims here. The ROLE_ prefix Spring required when building
        // authorities was a framework convention and does not survive the port.
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role.ToDbValue())));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationScheme));
    }
}
