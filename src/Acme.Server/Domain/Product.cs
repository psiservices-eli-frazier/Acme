namespace Acme.Server.Domain;

/// <summary>
/// A sellable product. Maps to <c>products</c>.
///
/// Note there are no stock rules anywhere in this application: <see cref="StockQuantity"/>
/// is a plain manually-edited number. Nothing decrements it when an order is placed,
/// reserves it, or checks availability. That is inherited from BrandX deliberately --
/// the requirements do not exist yet, and inventing them during a port would be
/// inventing business rules.
/// </summary>
public class Product
{
    public long Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public bool Active { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Label used in the order form's product dropdown.</summary>
    public string DisplayName => $"{Sku} - {Name}";
}
