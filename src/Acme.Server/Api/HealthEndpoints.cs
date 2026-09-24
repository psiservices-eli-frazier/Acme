using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Acme.Server.Api;

public static class HealthEndpoints
{
    public static void MapHealth(this IEndpointRouteBuilder routes)
    {
        // Unauthenticated on purpose, unlike every other route: load balancers and
        // orchestrators poll this without a session cookie, and it has to answer before
        // anyone could sign in.
        routes.MapHealthChecks("/api/health", new HealthCheckOptions { ResponseWriter = WriteResponse })
            .AllowAnonymous();
    }

    // The health check middleware already sets the response status code from
    // HealthCheckOptions.ResultStatusCodes (200 healthy/degraded, 503 unhealthy) before
    // calling this -- it only needs a body.
    private static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
            }),
        });
    }
}
