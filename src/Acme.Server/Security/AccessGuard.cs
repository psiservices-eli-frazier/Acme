using System.Security.Claims;
using Acme.Server.Services;
using Microsoft.AspNetCore.Authorization;

namespace Acme.Server.Security;

public static class Policies
{
    /// <summary>Product and customer writes, and order deletion.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Order create and update -- the day-to-day work STAFF exists to do.</summary>
    public const string AdminOrStaff = "AdminOrStaff";
}

/// <summary>
/// The authorization check the services call.
///
/// This sits at the service layer, not on the endpoints, and that placement is the
/// point. BrandX put <c>@PreAuthorize</c> on the service because that is the boundary a
/// second endpoint, a background job or a future API would also cross; the
/// <c>sec:authorize</c> attributes in its templates only hid buttons. The same split
/// holds here: the endpoints require authentication, the React components hide controls,
/// and this is the control.
///
/// A guard that exists only in the client is missing.
/// </summary>
public interface IAccessGuard
{
    ClaimsPrincipal? User { get; }

    /// <summary>Throws <see cref="ForbiddenException"/> unless the caller satisfies the policy.</summary>
    Task RequireAsync(string policy);
}

public sealed class AccessGuard(IHttpContextAccessor accessor, IAuthorizationService authorization) : IAccessGuard
{
    public ClaimsPrincipal? User => accessor.HttpContext?.User;

    public async Task RequireAsync(string policy)
    {
        var user = User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new ForbiddenException();
        }

        var result = await authorization.AuthorizeAsync(user, policy);
        if (!result.Succeeded)
        {
            throw new ForbiddenException();
        }
    }
}
