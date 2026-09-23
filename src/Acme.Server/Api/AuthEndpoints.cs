using System.Security.Claims;
using Acme.Server.Contracts;
using Acme.Server.Domain;
using Acme.Server.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Acme.Server.Api;

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder routes)
    {
        // Sign-in is inside the antiforgery filter too: Spring Security's CSRF filter
        // covered the login POST, so the client must fetch a token before signing in.
        var group = routes.MapGroup("/api/auth").AddEndpointFilter<AntiforgeryFilter>();

        group.MapPost("/login", async (
            LoginInput input,
            SignInService auth,
            HttpContext http,
            CancellationToken ct) =>
        {
            var principal = await auth.AuthenticateAsync(
                input.Username, input.Password, CookieAuthenticationDefaults.AuthenticationScheme, ct);

            if (principal is null)
            {
                // One message whichever half was wrong. Distinguishing "no such user"
                // from "wrong password" turns the form into an account oracle.
                return Results.Problem(
                    detail: "Incorrect username or password.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Sign-in failed");
            }

            // Rotates the session cookie, which is the session-fixation defence Spring
            // Security gave for free.
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Results.Ok(Describe(principal));
        }).AllowAnonymous();

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).AllowAnonymous();

        // The SPA calls this on boot to find out who it is talking to. 401 when signed
        // out is the signal to show the login screen.
        group.MapGet("/me", (HttpContext http) =>
            http.User.Identity?.IsAuthenticated == true
                ? Results.Ok(Describe(http.User))
                : Results.Unauthorized())
            .AllowAnonymous();

        // Issues the antiforgery cookie and hands back the token for the request
        // header. Must be called before the first unsafe request, including sign-in.
        routes.MapGet("/api/antiforgery/token", (IAntiforgery antiforgery, HttpContext http) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            return Results.Ok(new { token = tokens.RequestToken });
        }).AllowAnonymous();
    }

    private static CurrentUserResponse Describe(ClaimsPrincipal principal)
    {
        var roles = principal.FindAll(ClaimTypes.Role)
            .Select(claim => RoleExtensions.FromDbValue(claim.Value))
            .ToList();

        return new CurrentUserResponse(
            principal.Identity?.Name ?? string.Empty,
            principal.FindFirstValue(SignInService.DisplayNameClaim) ?? string.Empty,
            roles);
    }
}
