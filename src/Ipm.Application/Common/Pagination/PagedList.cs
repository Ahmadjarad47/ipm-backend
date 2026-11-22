namespace Ipm.Application.Common.Pagination;

/// <summary>Standard paged response: <c>{ items, page, pageSize, total }</c>.</summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public bool HasNextPage => Page * PageSize < Total;
}

/// <summary>Paging input shared by list queries. Page is 1-based, page size is capped.</summary>
public sealed record PageRequest(int Page = 1, int PageSize = 50)
{
    public const int MaxPageSize = 200;

    public int Skip => (Math.Max(Page, 1) - 1) * Take;

    public int Take => Math.Clamp(PageSize, 1, MaxPageSize);
}
