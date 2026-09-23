namespace Acme.Server.Data;

/// <summary>
/// A resolved page request. Pages are zero-based and the default size is 10, matching
/// BrandX's <c>@PageableDefault(size = 10)</c> -- the query-string contract is identical
/// so the SPA's URL state works the same way the Thymeleaf links did.
/// </summary>
public sealed record PageRequest(int Page, int Size, string SortProperty, bool Descending)
{
    public const int DefaultSize = 10;

    /// <summary>
    /// Spring Data silently clamped oversized page requests at 2000; keep the same cap
    /// so a hand-crafted <c>?size=100000</c> cannot ask the database for everything.
    /// </summary>
    public const int MaxSize = 2000;

    public int Offset => Page * Size;

    /// <summary>The <c>property,direction</c> form echoed back to the client.</summary>
    public string SortParam => $"{SortProperty},{(Descending ? "desc" : "asc")}";
}

/// <summary>
/// One page of results. Mirrors the fields the Thymeleaf pager fragment read off
/// Spring's <c>Page</c>, so the React pagination component renders from the same shape.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Content, int Page, int Size, long TotalElements)
{
    public int TotalPages => Size <= 0 ? 0 : (int)Math.Ceiling(TotalElements / (double)Size);

    public bool First => Page == 0;

    public bool Last => Page >= TotalPages - 1;
}
