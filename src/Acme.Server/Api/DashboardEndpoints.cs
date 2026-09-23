using Acme.Server.Contracts;
using Acme.Server.Services;

namespace Acme.Server.Api;

public static class DashboardEndpoints
{
    public static void MapDashboard(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/dashboard", async (
            ProductService productService,
            CustomerService customerService,
            OrderService orderService,
            CancellationToken ct) =>
        {
            var products = await productService.CountAsync(ct);
            var customers = await customerService.CountAsync(ct);
            var orders = await orderService.CountAsync(ct);

            return Results.Ok(new DashboardResponse(products, customers, orders));
        }).RequireAuthorization();
    }
}
