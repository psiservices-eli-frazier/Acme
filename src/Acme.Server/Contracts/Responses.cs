using Acme.Server.Data;
using Acme.Server.Domain;

namespace Acme.Server.Contracts;

/// <summary>
/// One page of results plus the filter state that produced it.
///
/// <c>search</c> and <c>sort</c> are echoed back for the same reason the Java
/// controllers put them in the model: list state lives in the URL, and every paging and
/// sorting link has to carry it forward rather than silently dropping the user's filter.
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Content,
    int Page,
    int Size,
    long TotalElements,
    int TotalPages,
    bool First,
    bool Last,
    string? Search,
    string Sort);

public static class PagedResponse
{
    public static PagedResponse<TOut> From<TIn, TOut>(
        PagedResult<TIn> result,
        PageRequest request,
        string? search,
        Func<TIn, TOut> map) =>
        new(
            [.. result.Content.Select(map)],
            result.Page,
            result.Size,
            result.TotalElements,
            result.TotalPages,
            result.First,
            result.Last,
            search,
            request.SortParam);
}

public sealed record ProductResponse(
    long Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    bool Active,
    string DisplayName,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static ProductResponse From(Product p) => new(
        p.Id, p.Sku, p.Name, p.Description, p.Price, p.StockQuantity, p.Active,
        p.DisplayName, p.CreatedAt, p.UpdatedAt);
}

public sealed record AddressResponse(
    string? Line1,
    string? Line2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country)
{
    public static AddressResponse From(Customer c) => new(
        c.AddressLine1, c.AddressLine2, c.AddressCity, c.AddressState, c.AddressPostalCode, c.AddressCountry);
}

/// <param name="OrderCount">
/// Only populated on the single-customer read; null in list results.
/// </param>
public sealed record CustomerResponse(
    long Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? Phone,
    string DisplayName,
    bool HasAddress,
    AddressResponse Address,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    long? OrderCount)
{
    public static CustomerResponse From(Customer c, long? orderCount = null) => new(
        c.Id, c.FirstName, c.LastName, c.FullName, c.Email, c.Phone, c.DisplayName,
        c.HasAddress, AddressResponse.From(c), c.CreatedAt, c.UpdatedAt, orderCount);
}

/// <summary>The customer summary shown on an order, not a full customer record.</summary>
public sealed record OrderCustomerResponse(long Id, string FullName, string Email)
{
    public static OrderCustomerResponse From(Customer c) => new(c.Id, c.FullName, c.Email);
}

/// <summary>
/// A row on the order list. <c>Total</c> comes from the aggregate query over the page's
/// order ids, not from summing lines -- the list does not load lines.
/// </summary>
public sealed record OrderListItemResponse(
    long Id,
    string OrderNumber,
    OrderCustomerResponse? Customer,
    OrderStatus Status,
    string StatusLabel,
    DateTime OrderedAt,
    decimal Total)
{
    public static OrderListItemResponse From(Order o, decimal total) => new(
        o.Id,
        o.OrderNumber,
        o.Customer is null ? null : OrderCustomerResponse.From(o.Customer),
        o.Status,
        o.Status.Label(),
        o.OrderedAt,
        total);
}

public sealed record OrderItemResponse(
    long Id,
    long ProductId,
    string ProductSku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal)
{
    public static OrderItemResponse From(OrderItem i) => new(
        i.Id, i.ProductId, i.Product?.Sku ?? string.Empty, i.Product?.Name ?? string.Empty,
        i.Quantity, i.UnitPrice, i.LineTotal);
}

public sealed record OrderResponse(
    long Id,
    string OrderNumber,
    OrderCustomerResponse? Customer,
    OrderStatus Status,
    string StatusLabel,
    DateTime OrderedAt,
    string? Notes,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Total,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static OrderResponse From(Order o) => new(
        o.Id,
        o.OrderNumber,
        o.Customer is null ? null : OrderCustomerResponse.From(o.Customer),
        o.Status,
        o.Status.Label(),
        o.OrderedAt,
        o.Notes,
        [.. o.Items.Select(OrderItemResponse.From)],
        o.Total,
        o.CreatedAt,
        o.UpdatedAt);
}

/// <summary>An entry in the order form's product dropdown.</summary>
public sealed record ProductOptionResponse(long Id, string DisplayName, decimal Price)
{
    public static ProductOptionResponse From(Product p) => new(p.Id, p.DisplayName, p.Price);
}

/// <summary>An entry in the order form's customer dropdown.</summary>
public sealed record CustomerOptionResponse(long Id, string DisplayName)
{
    public static CustomerOptionResponse From(Customer c) => new(c.Id, c.DisplayName);
}

/// <summary>An entry in the order form's status dropdown.</summary>
public sealed record OrderStatusOptionResponse(OrderStatus Value, string Label)
{
    public static IReadOnlyList<OrderStatusOptionResponse> All { get; } =
        [.. Enum.GetValues<OrderStatus>().Select(s => new OrderStatusOptionResponse(s, s.Label()))];
}

public sealed record DashboardResponse(long ProductCount, long CustomerCount, long OrderCount);

public sealed record CurrentUserResponse(string Username, string DisplayName, IReadOnlyList<Role> Roles);
