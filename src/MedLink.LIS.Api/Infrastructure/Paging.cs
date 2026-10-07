// Пагінація: ?page=1&pageSize=50&sort=field&dir=asc → { items, total, page, pageSize }
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Infrastructure;

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class PagingQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Sort { get; set; }
    public string? Dir { get; set; }

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize < 1 ? 50 : Math.Min(PageSize, 500);
    public bool Desc => string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase);
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, PagingQuery paging, CancellationToken ct = default)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((paging.SafePage - 1) * paging.SafePageSize).Take(paging.SafePageSize).ToListAsync(ct);
        return new PagedResult<T> { Items = items, Total = total, Page = paging.SafePage, PageSize = paging.SafePageSize };
    }

    public static PagedResult<T> ToPaged<T>(this IEnumerable<T> items, PagingQuery paging)
    {
        var list = items.ToList();
        return new PagedResult<T>
        {
            Items = list.Skip((paging.SafePage - 1) * paging.SafePageSize).Take(paging.SafePageSize).ToList(),
            Total = list.Count,
            Page = paging.SafePage,
            PageSize = paging.SafePageSize
        };
    }
}
