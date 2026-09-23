namespace Acme.Server.Domain;

/// <summary>
/// A customer. Maps to <c>customers</c>.
///
/// The address columns are flat properties here rather than a nested value object, even
/// though Java modelled them as an <c>@Embeddable</c>. The table is flat either way, and
/// keeping the domain type flat means every query maps with plain column aliases instead
/// of relying on Insight's one-to-one record splitting. The nested shape the Java
/// templates saw is reconstructed in the API contract, which is where it is actually
/// useful (see <c>Contracts/AddressDto</c>).
///
/// There is deliberately no <c>Orders</c> collection: code that needs a customer's orders
/// goes through the order repository, the same as in Acme.
/// </summary>
public class Customer
{
    public long Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? AddressCity { get; set; }

    public string? AddressState { get; set; }

    public string? AddressPostalCode { get; set; }

    public string? AddressCountry { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Label used in the order form's customer dropdown.</summary>
    public string DisplayName => $"{FullName} ({Email})";

    /// <summary>
    /// Mirrors the Java <c>Address.isEmpty()</c>: true when every address column is
    /// null or blank, so the detail screen can skip the whole block.
    /// </summary>
    public bool HasAddress =>
        !string.IsNullOrWhiteSpace(AddressLine1)
        || !string.IsNullOrWhiteSpace(AddressLine2)
        || !string.IsNullOrWhiteSpace(AddressCity)
        || !string.IsNullOrWhiteSpace(AddressState)
        || !string.IsNullOrWhiteSpace(AddressPostalCode)
        || !string.IsNullOrWhiteSpace(AddressCountry);
}
