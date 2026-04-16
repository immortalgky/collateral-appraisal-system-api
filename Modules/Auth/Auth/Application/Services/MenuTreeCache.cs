using Auth.Domain.Menu;
using Auth.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Auth.Application.Services;

public class MenuTreeCache(AuthDbContext dbContext, IMemoryCache cache) : IMenuTreeCache
{
    private const string CacheKey = "auth:menu:full";

    public async Task<IReadOnlyList<MenuItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<MenuItem>? cached) && cached is not null)
            return cached;

        var items = await dbContext.MenuItems
            .AsNoTracking()
            .Include(m => m.Translations)
            .OrderBy(m => m.Scope)
            .ThenBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);

        cache.Set(CacheKey, (IReadOnlyList<MenuItem>)items);
        return items;
    }

    public void Invalidate()
    {
        cache.Remove(CacheKey);
    }
}
