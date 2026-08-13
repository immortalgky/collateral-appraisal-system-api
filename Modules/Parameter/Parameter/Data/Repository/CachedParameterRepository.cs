using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Parameter.Data.Repository;

public class CachedParameterRepository(IParameterRepository inner, IMemoryCache cache) : IParameterRepository
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static CancellationTokenSource _cts = new();

    public async Task<List<Parameters.Models.Parameter>> GetParameter(
        ParameterDto request, bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"parameter:{request.ParId}:{request.Group}:{request.Country}:{request.Language}:{request.Code}:{request.Description}:{request.IsActive}:{request.SeqNo}";
        if (cache.TryGetValue(cacheKey, out List<Parameters.Models.Parameter>? cached))
            return cached!;

        var result = await inner.GetParameter(request, asNoTracking, cancellationToken);

        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheDuration)
            .AddExpirationToken(new CancellationChangeToken(_cts.Token));

        cache.Set(cacheKey, result, options);

        return result;
    }

    public async Task<Parameters.Models.Parameter?> GetParameterByParId(
        long parId,
        CancellationToken cancellationToken = default)
    {
        return await inner.GetParameterByParId(parId, cancellationToken);
    }

    public async Task AddAsync(
        Parameters.Models.Parameter parameter,
        CancellationToken cancellationToken = default)
    {
        InvalidateCache();
        await inner.AddAsync(parameter, cancellationToken);
    }

    public async Task DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        InvalidateCache();
        await inner.DeleteAsync(id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await inner.SaveChangesAsync(cancellationToken);
        InvalidateCache();
    }

    private static void InvalidateCache()
    {
        var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }
}