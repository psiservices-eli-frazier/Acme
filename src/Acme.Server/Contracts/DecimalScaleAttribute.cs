using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Acme.Server.Contracts;

/// <summary>
/// Rejects a decimal with more fractional digits than the column can hold. Stands in
/// for Jakarta's <c>@Digits(fraction = 2)</c>, which had no Data Annotations equivalent.
///
/// Worth having rather than letting the value round on the way to a
/// <c>decimal(19, 2)</c> column: silently changing a submitted price is worse than
/// refusing it.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class DecimalScaleAttribute(int scale) : ValidationAttribute
{
    public int Scale { get; } = scale;

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true; // absence is [Required]'s business, not ours
        }

        if (value is not decimal number)
        {
            return true;
        }

        return decimal.Round(number, Scale) == number;
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage
        ?? string.Format(
            CultureInfo.InvariantCulture,
            "The field {0} must have no more than {1} decimal place(s).",
            name,
            Scale);
}
