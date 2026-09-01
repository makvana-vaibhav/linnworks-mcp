namespace LinnworksMcp.Models;

/// <summary>
/// Uniform pagination envelope returned by every list-style tool.
/// </summary>
/// <remarks>
/// Linnworks is not consistent about paging — some endpoints are page-based
/// (<c>PageNumber</c>/<c>EntriesPerPage</c>), some offset-based (<c>startIndex</c>/<c>itemsCount</c>),
/// and some only accept a row cap (<c>numRows</c>). Services normalise all three into this shape
/// so MCP clients see one contract; each tool's description says which style sits underneath.
/// </remarks>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>1-based page number this result represents.</summary>
    public required int PageNumber { get; init; }

    public required int PageSize { get; init; }

    /// <summary>Total matching records, when the upstream endpoint reports it.</summary>
    public int? TotalCount { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>
    /// Set when the upstream endpoint cannot page and returned a capped result set, so a client
    /// knows a larger page size — not a later page — is the way to see more.
    /// </summary>
    public string? PagingNote { get; init; }

    public static PagedResult<T> Create(
        IReadOnlyList<T> items,
        int pageNumber,
        int pageSize,
        int? totalCount = null,
        string? pagingNote = null) =>
        new()
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            // Without a total, a full page is the only signal that more may exist.
            HasMore = totalCount.HasValue
                ? (long)pageNumber * pageSize < totalCount.Value
                : items.Count == pageSize,
            PagingNote = pagingNote
        };
}
