using System.ComponentModel.DataAnnotations;
using Acme.Server.Domain;
using Microsoft.Extensions.Validation;

namespace Acme.Server.Contracts;

/// <summary>
/// Create/update payload for a product.
///
/// There is deliberately no <c>Id</c>: the identity of the thing being written comes
/// from the route, never the body. In Java this mattered because saving a
/// request-bound entity with a non-null id was treated as a merge, letting a crafted
/// POST overwrite an arbitrary row. The hazard here is the same shape -- an UPDATE
/// keyed off body data -- and the defence is the same: the body cannot name a row.
/// </summary>
public sealed class ProductInput
{
    [Required]
    [StringLength(64)]
    public string Sku { get; init; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    [Required]
    [Range(typeof(decimal), "0.00", "99999999999999999.99", ErrorMessage = "Price must not be negative")]
    [DecimalScale(2)]
    public decimal? Price { get; init; }

    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity must not be negative")]
    public int? StockQuantity { get; init; }

    public bool Active { get; init; } = true;
}

public sealed class AddressInput
{
    [StringLength(200)]
    public string? Line1 { get; init; }

    [StringLength(200)]
    public string? Line2 { get; init; }

    [StringLength(100)]
    public string? City { get; init; }

    [StringLength(100)]
    public string? State { get; init; }

    [StringLength(20)]
    public string? PostalCode { get; init; }

    [StringLength(100)]
    public string? Country { get; init; }
}

[ValidatableType]
public sealed class CustomerInput
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; init; } = string.Empty;

    [StringLength(40)]
    public string? Phone { get; init; }

    public AddressInput? Address { get; init; }
}

public sealed class OrderItemInput
{
    [Required(ErrorMessage = "Select a product")]
    public long? ProductId { get; init; }

    [Required(ErrorMessage = "Enter a quantity")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int? Quantity { get; init; } = 1;
}

/// <summary>
/// Create/update payload for an order.
///
/// Note what is absent: no id, no order number, no unit prices. The order number is
/// allocated server-side on create and never regenerated; line prices are decided
/// server-side and never trusted from the request. Line ids are absent too, because
/// they would be meaningless -- every edit replaces an order's lines wholesale.
/// </summary>
[ValidatableType]
public sealed class OrderInput
{
    [Required(ErrorMessage = "Select a customer")]
    public long? CustomerId { get; init; }

    [Required(ErrorMessage = "Select a status")]
    public OrderStatus? Status { get; init; } = OrderStatus.New;

    [StringLength(2000)]
    public string? Notes { get; init; }

    [MinLength(1, ErrorMessage = "An order needs at least one line item")]
    public List<OrderItemInput> Items { get; init; } = [];
}

public sealed class LoginInput
{
    [Required]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
