using Acme.Server.Contracts;
using Acme.Server.Data;
using Acme.Server.Services;

namespace Acme.Server.Api;

public static class CustomerEndpoints
{
    public static void MapCustomers(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/customers")
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryFilter>();

        group.MapGet("/", async (
            string? search,
            int? page,
            int? size,
            string? sort,
            CustomerService service,
            CancellationToken ct) =>
        {
            var request = PageRequestParsing.Parse(SortMap.Customers, page, size, sort);
            var result = await service.ListAsync(search, request, ct);
            return Results.Ok(PagedResponse.From(
                result, request, search, customer => CustomerResponse.From(customer)));
        });

        group.MapGet("/lookup", async (CustomerService service, CancellationToken ct) =>
        {
            var customers = await service.ForSelectionAsync(ct);
            return Results.Ok(customers.Select(CustomerOptionResponse.From));
        });

        // The order count rides along on the single read, because the detail screen
        // shows it and would otherwise need a second call.
        group.MapGet("/{id:long}", async (long id, CustomerService service, CancellationToken ct) =>
        {
            var customer = await service.GetAsync(id, ct);
            var orderCount = await service.OrderCountAsync(id, ct);
            return Results.Ok(CustomerResponse.From(customer, orderCount));
        });

        group.MapPost("/", async (CustomerInput input, CustomerService service, CancellationToken ct) =>
        {
            var customer = await service.CreateAsync(input, ct);
            return Results.Created($"/api/customers/{customer.Id}", CustomerResponse.From(customer));
        });

        group.MapPut("/{id:long}", async (long id, CustomerInput input, CustomerService service, CancellationToken ct) =>
            Results.Ok(CustomerResponse.From(await service.UpdateAsync(id, input, ct))));

        group.MapDelete("/{id:long}", async (long id, CustomerService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
