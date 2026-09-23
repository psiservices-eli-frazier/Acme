using Acme.Server.Contracts;
using Acme.Server.Data;
using Acme.Server.Services;

namespace Acme.Server.Api;

public static class ProductEndpoints
{
    public static void MapProducts(this IEndpointRouteBuilder routes)
    {
        // RequireAuthorization with no policy means "signed in", matching the Java
        // filter chain where every screen needed a session and nothing more. Role
        // checks live in the service -- see AccessGuard.
        var group = routes.MapGroup("/api/products")
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryFilter>();

        group.MapGet("/", async (
            string? search,
            int? page,
            int? size,
            string? sort,
            ProductService service,
            CancellationToken ct) =>
        {
            var request = PageRequestParsing.Parse(SortMap.Products, page, size, sort);
            var result = await service.ListAsync(search, request, ct);
            return Results.Ok(PagedResponse.From(result, request, search, ProductResponse.From));
        });

        group.MapGet("/active", async (ProductService service, CancellationToken ct) =>
        {
            var active = await service.ActiveAsync(ct);
            return Results.Ok(active.Select(ProductOptionResponse.From));
        });

        group.MapGet("/{id:long}", async (long id, ProductService service, CancellationToken ct) =>
            Results.Ok(ProductResponse.From(await service.GetAsync(id, ct))));

        group.MapPost("/", async (ProductInput input, ProductService service, CancellationToken ct) =>
        {
            var product = await service.CreateAsync(input, ct);
            return Results.Created($"/api/products/{product.Id}", ProductResponse.From(product));
        });

        // The id comes from the route. The body has no id field to disagree with it.
        group.MapPut("/{id:long}", async (long id, ProductInput input, ProductService service, CancellationToken ct) =>
            Results.Ok(ProductResponse.From(await service.UpdateAsync(id, input, ct))));

        group.MapDelete("/{id:long}", async (long id, ProductService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
