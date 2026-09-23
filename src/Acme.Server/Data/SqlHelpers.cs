using System.Globalization;

namespace Acme.Server.Data;

internal static class SqlHelpers
{
    /// <summary>
    /// Builds an <c>IN (1, 2, 3)</c> fragment from a set of ids.
    ///
    /// The ids are written as literals rather than parameters, which needs a word of
    /// justification. Two things rule the obvious alternatives out:
    ///
    ///   * Insight's list-parameter support is provider-specific (table-valued
    ///     parameters on SQL Server), so it is not portable across the four dialects.
    ///   * Passing an <c>IDictionary&lt;string, object&gt;</c> of @p0..@pN is not safe:
    ///     Insight caches a parameter-generator delegate per SQL string on that path,
    ///     and the cached delegate holds the command it was built from. The first call
    ///     succeeds and every later call with the same SQL throws ObjectDisposedException
    ///     on a disposed command. Typed parameter objects do not take that path.
    ///
    /// These values are <c>long</c>s the application just read out of its own database,
    /// not text from a request, so there is nothing to escape -- the type is the
    /// guarantee. Never widen this helper to strings.
    ///
    /// Never call it with an empty collection either: <c>IN ()</c> is a syntax error on
    /// several engines. Callers short-circuit first, as the Java service did.
    /// </summary>
    public static string InClause(IReadOnlyCollection<long> ids)
    {
        ArgumentOutOfRangeException.ThrowIfZero(ids.Count);
        return $"({string.Join(", ", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)))})";
    }

    /// <summary>
    /// Wraps a search term for a <c>LIKE</c> comparison. The wildcards go on in C# and
    /// the comparison is written <c>lower(col) LIKE lower(@term)</c>, which behaves the
    /// same on every engine -- no ILIKE, no collation assumptions.
    /// </summary>
    public static string Contains(string term) => $"%{term.Trim()}%";
}
