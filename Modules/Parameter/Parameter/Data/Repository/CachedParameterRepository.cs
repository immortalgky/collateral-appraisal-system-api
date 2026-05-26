using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Parameter.Data.Repository;

public class CachedParameterRepository(
    IParameterRepository inner,
    IMemoryCache cache,
    ParameterCacheInvalidator invalidator) : IParameterRepository
{
    private CancellationTokenSource _cts = new();

    private MemoryCacheEntryOptions CacheOptions => new MemoryCacheEntryOptions()
    .AddExpirationToken(new CancellationChangeToken(invalidator.Token));


    public async Task<List<Parameters.Models.Parameter>> GetParameter(
        ParameterDto request, bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"parameter:{request.ParId}:{request.Group}:{request.Country}:{request.Language}:{request.Code}:{request.Description}:{request.IsActive}:{request.SeqNo}";

        if (cache.TryGetValue(cacheKey, out List<Parameters.Models.Parameter>? cached))
            return cached!;

        var result = await inner.GetParameter(request, asNoTracking, cancellationToken);

        cache.Set(cacheKey, result, CacheOptions);

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
        await inner.AddAsync(parameter, cancellationToken);
    }

    public async Task DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await inner.DeleteAsync(id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await inner.SaveChangesAsync(cancellationToken);
        invalidator.InvalidateAll();
    }
}


public class ParameterCacheInvalidator
{
    private CancellationTokenSource _cts = new();

    public CancellationToken Token => _cts.Token;

    public void InvalidateAll()
{    var oldCts = _cts;
    _cts = new CancellationTokenSource();
    oldCts.Cancel();
    oldCts.Dispose();
}
}