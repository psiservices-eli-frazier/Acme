using Acme.Server.Contracts;
using Acme.Server.Data;
using Acme.Server.Services;

namespace Acme.Server.Api;

public static class OrderEndpoints
{
    public static void MapOrders(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/orders")
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryFilter>();

        group.MapGet("/", async (
            string? search,
            long? customerId,
            int? page,
            int? size,
            string? sort,
            OrderService service,
            CancellationToken ct) =>
        {
            var request = PageRequestParsing.Parse(SortMap.Orders, page, size, sort);
            var result = await service.ListAsync(search, customerId, request, ct);

            // One aggregate query over this page's ids. The list query does not load
            // lines, so the rows cannot sum them themselves.
            var totals = await service.TotalsForAsync([.. result.Content.Select(o => o.Id)], ct);

            return Results.Ok(PagedResponse.From(
                result,
                request,
                search,
                order => OrderListItemResponse.From(order, totals.GetValueOrDefault(order.Id))));
        });

        group.MapGet("/statuses", () => Results.Ok(OrderStatusOptionResponse.All));

        group.MapGet("/{id:long}", async (long id, OrderService service, CancellationToken ct) =>
            Results.Ok(OrderResponse.From(await service.GetDetailAsync(id, ct))));

        group.MapPost("/", async (OrderInput input, OrderService service, CancellationToken ct) =>
        {
            var order = await service.CreateAsync(input, ct);
            return Results.Created($"/api/orders/{order.Id}", OrderResponse.From(order));
        });

        group.MapPut("/{id:long}", async (long id, OrderInput input, OrderService service, CancellationToken ct) =>
            Results.Ok(OrderResponse.From(await service.UpdateAsync(id, input, ct))));

        group.MapDelete("/{id:long}", async (long id, OrderService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
