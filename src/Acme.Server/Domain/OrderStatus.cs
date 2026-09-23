namespace Acme.Server.Domain;

/// <summary>
/// Order lifecycle state. Persisted as the upper-case name ("NEW", "PAID", ...) to
/// match the string form Hibernate's <c>EnumType.STRING</c> wrote, so this app can be
/// pointed at an existing BrandX database without a data migration.
/// </summary>
public enum OrderStatus
{
    New,
    Paid,
    Shipped,
    Delivered,
    Cancelled,
}

public static class OrderStatusExtensions
{
    /// <summary>The value stored in <c>orders.status</c>.</summary>
    public static string ToDbValue(this OrderStatus status) => status.ToString().ToUpperInvariant();

    /// <summary>
    /// The human-readable label. Identical to the PascalCase name for every current
    /// member, but kept as its own concept because the Java enum carried one.
    /// </summary>
    public static string Label(this OrderStatus status) => status.ToString();

    public static OrderStatus FromDbValue(string value) =>
        Enum.Parse<OrderStatus>(value, ignoreCase: true);
}
