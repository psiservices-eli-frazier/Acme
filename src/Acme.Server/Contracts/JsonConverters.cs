using System.Text.Json;
using System.Text.Json.Serialization;
using Acme.Server.Domain;

namespace Acme.Server.Contracts;

/// <summary>
/// Serialises <see cref="OrderStatus"/> as the upper-case name ("NEW", "PAID", ...).
///
/// That is the form the Java API produced, the form stored in the database, and the
/// form the ported stylesheet expects -- the status pill's class is literally
/// <c>status-NEW</c>. Keeping one spelling end to end avoids a mapping table in the
/// client.
/// </summary>
public sealed class OrderStatusJsonConverter : JsonConverter<OrderStatus>
{
    public override OrderStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Order status is required.");
        }

        if (!Enum.TryParse<OrderStatus>(value, ignoreCase: true, out var status))
        {
            throw new JsonException($"'{value}' is not a valid order status.");
        }

        return status;
    }

    public override void Write(Utf8JsonWriter writer, OrderStatus value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToDbValue());
}

/// <summary>Serialises <see cref="Role"/> as "ADMIN" / "STAFF", for the same reason.</summary>
public sealed class RoleJsonConverter : JsonConverter<Role>
{
    public override Role Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value) || !Enum.TryParse<Role>(value, ignoreCase: true, out var role))
        {
            throw new JsonException($"'{value}' is not a valid role.");
        }

        return role;
    }

    public override void Write(Utf8JsonWriter writer, Role value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToDbValue());
}
