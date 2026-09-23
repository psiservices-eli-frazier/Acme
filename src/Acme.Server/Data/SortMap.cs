namespace Acme.Server.Data;

/// <summary>
/// The set of columns a list screen may be sorted by, and the default.
///
/// Acme has no such allow-list: <c>sort</c> went straight into the JPA query, so
/// <c>?sort=bogus</c> was a 500 and <c>?sort=customer.email</c> quietly traversed a
/// relation. The effective whitelist was just the column links the templates happened
/// to render. Those exact columns are encoded here, and anything else is rejected.
/// </summary>
public sealed class SortMap
{
    private readonly IReadOnlyDictionary<string, string> _columns;

    private SortMap(string defaultProperty, bool defaultDescending, IReadOnlyDictionary<string, string> columns)
    {
        DefaultProperty = defaultProperty;
        DefaultDescending = defaultDescending;
        _columns = columns;
    }

    public string DefaultProperty { get; }

    public bool DefaultDescending { get; }

    public IEnumerable<string> AllowedProperties => _columns.Keys;

    public bool TryResolveColumn(string property, out string column) =>
        _columns.TryGetValue(property, out column!);

    /// <summary>
    /// Resolves a requested sort property, also handing back the canonical spelling.
    ///
    /// The canonical form matters: the client compares the echoed <c>sort</c> value
    /// against a literal when deciding which way a column header toggles, so
    /// <c>?sort=STOCKQUANTITY</c> has to come back as <c>stockQuantity,asc</c> or the
    /// toggle sticks.
    /// </summary>
    public bool TryResolve(string property, out string canonicalProperty, out string column)
    {
        foreach (var (key, value) in _columns)
        {
            if (string.Equals(key, property, StringComparison.OrdinalIgnoreCase))
            {
                canonicalProperty = key;
                column = value;
                return true;
            }
        }

        canonicalProperty = string.Empty;
        column = string.Empty;
        return false;
    }

    private static SortMap Create(
        string defaultProperty,
        bool defaultDescending,
        params (string Property, string Column)[] columns) =>
        new(
            defaultProperty,
            defaultDescending,
            columns.ToDictionary(c => c.Property, c => c.Column, StringComparer.OrdinalIgnoreCase));

    /// <summary>Sortable columns on the product list. Default <c>name,asc</c>.</summary>
    public static readonly SortMap Products = Create(
        "name",
        defaultDescending: false,
        ("sku", "p.sku"),
        ("name", "p.name"),
        ("price", "p.price"),
        ("stockQuantity", "p.stock_quantity"));

    /// <summary>
    /// Sortable columns on the customer list. Default <c>lastName,asc</c>. The Name
    /// column sorts on last name alone, as it did in Java.
    /// </summary>
    public static readonly SortMap Customers = Create(
        "lastName",
        defaultDescending: false,
        ("lastName", "c.last_name"),
        ("email", "c.email"));

    /// <summary>
    /// Sortable columns on the order list. Default <c>orderedAt,desc</c>. Sorting by
    /// status orders by the stored string ("CANCELLED", "DELIVERED", "NEW", ...), which
    /// is what Hibernate's EnumType.STRING produced too.
    /// </summary>
    public static readonly SortMap Orders = Create(
        "orderedAt",
        defaultDescending: true,
        ("orderNumber", "o.order_number"),
        ("status", "o.status"),
        ("orderedAt", "o.ordered_at"));
}
