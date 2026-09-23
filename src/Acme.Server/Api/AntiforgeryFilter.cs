using Microsoft.AspNetCore.Antiforgery;

namespace Acme.Server.Api;

/// <summary>
/// Validates the antiforgery token on every state-changing request.
///
/// <c>UseAntiforgery()</c> only validates endpoints that accept form data, so a JSON
/// API needs this explicitly. It is applied to every API group including sign-in --
/// Spring Security's CSRF filter covered the login POST too, and a Java test asserts
/// the protection holds even for an admin.
/// </summary>
public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        if (!SafeMethods.Contains(http.Request.Method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(http);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(
                    detail: "Missing or invalid antiforgery token.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Not permitted");
            }
        }

        return await next(context);
    }
}
