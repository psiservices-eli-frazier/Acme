namespace Acme.Server.Domain;

/// <summary>
/// One line of an order. Maps to <c>order_items</c>.
///
/// <see cref="UnitPrice"/> is a price snapshot taken when the line is first created and
/// preserved across later edits, so repricing a product does not silently restate order
/// history. See <c>OrderService.ApplyFormAsync</c> for the rule that maintains it.
///
/// Line identity does not survive an edit: every update deletes and reinserts all of an
/// order's lines, so <see cref="Id"/> is not stable and a submitted line id is ignored.
/// </summary>
public class OrderItem
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public long ProductId { get; set; }

    public int Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    /// <summary>Populated only by the detail load.</summary>
    public Product? Product { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
