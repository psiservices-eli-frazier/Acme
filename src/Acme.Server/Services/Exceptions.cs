namespace Acme.Server.Services;

/// <summary>
/// "This thing does not exist." Becomes a 404 with the message in <c>detail</c>.
/// </summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>
/// A uniqueness rule refused the write. Carries the field it belongs to so the API can
/// return it as a field-level validation error and the form can render it next to the
/// offending input -- the job <c>BindingResult.rejectValue</c> did in Java.
/// </summary>
public sealed class DuplicateValueException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

/// <summary>
/// A referential rule refused the delete. Becomes a 409, which the client shows as a
/// banner on the screen the user was already on -- not an error page.
/// </summary>
public sealed class EntityInUseException(string message) : Exception(message);

/// <summary>
/// The signed-in user may not do this. Becomes a 403, kept deliberately distinct from
/// 404 so a refusal never implies the record is missing.
/// </summary>
public sealed class ForbiddenException(string message = "Not permitted") : Exception(message);

/// <summary>
/// The requested sort column is not on the allow-list. Becomes a 400 keyed to
/// <c>sort</c>.
///
/// Acme had no allow-list at all -- the value went straight into the JPA query, so an
/// unknown property was a 500 and a dotted one traversed a relation.
/// </summary>
public sealed class InvalidSortException(string property, IEnumerable<string> allowed)
    : Exception($"'{property}' is not a sortable column. Allowed: {string.Join(", ", allowed)}.");
