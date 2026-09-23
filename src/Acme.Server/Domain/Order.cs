namespace Acme.Server.Domain;

/// <summary>
/// An order. Maps to <c>orders</c>.
///
/// <see cref="OrderNumber"/> is a separate human-friendly identifier from the primary
/// key, allocated once on create and never regenerated. <see cref="OrderedAt"/> is
/// likewise set once on create and never touched by an update.
/// </summary>
public class Order
{
    public long Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public long CustomerId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.New;

    public DateTime OrderedAt { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Populated only by the queries that join it -- the paged list and the detail
    /// load. Null elsewhere; nothing here lazy-loads.
    /// </summary>
    public Customer? Customer { get; set; }

    /// <summary>Populated only by the detail load.</summary>
    public List<OrderItem> Items { get; set; } = [];

    /// <summary>
    /// Derived rather than stored: a persisted total is a second source of truth that
    /// drifts the first time a line changes without it being recalculated.
    ///
    /// Only meaningful when <see cref="Items"/> was actually loaded. The list screen
    /// does not load lines and gets its totals from one aggregate query instead.
    /// </summary>
    public decimal Total => Items.Sum(item => item.LineTotal);

    public int ItemCount => Items.Count;
}
