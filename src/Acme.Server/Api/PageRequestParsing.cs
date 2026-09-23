using Acme.Server.Data;
using Acme.Server.Services;

namespace Acme.Server.Api;

internal static class PageRequestParsing
{
    /// <summary>
    /// Resolves the <c>?page=&amp;size=&amp;sort=</c> triple against a screen's sort
    /// allow-list.
    ///
    /// The contract is deliberately identical to the one Spring Data produced, so the
    /// SPA's URLs look and behave like the Thymeleaf ones: pages are zero-based, the
    /// default size is 10, and <c>sort</c> is <c>property,direction</c>.
    /// </summary>
    public static PageRequest Parse(SortMap map, int? page, int? size, string? sort)
    {
        var resolvedPage = Math.Max(page ?? 0, 0);
        var resolvedSize = Math.Clamp(size ?? PageRequest.DefaultSize, 1, PageRequest.MaxSize);

        if (string.IsNullOrWhiteSpace(sort))
        {
            return new PageRequest(resolvedPage, resolvedSize, map.DefaultProperty, map.DefaultDescending);
        }

        var parts = sort.Split(',', 2);
        var requested = parts[0].Trim();
        var descending = parts.Length > 1
            && parts[1].Trim().Equals("desc", StringComparison.OrdinalIgnoreCase);

        if (!map.TryResolve(requested, out var canonical, out _))
        {
            throw new InvalidSortException(requested, map.AllowedProperties);
        }

        return new PageRequest(resolvedPage, resolvedSize, canonical, descending);
    }
}
