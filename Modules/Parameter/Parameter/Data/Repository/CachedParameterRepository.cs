using Microsoft.Extensions.Caching.Memory;

namespace Parameter.Data.Repository;

public class CachedParameterRepository(IParameterRepository inner, IMemoryCache cache) : IParameterRepository
{
    public async Task<List<Parameters.Models.Parameter>> GetParameter(ParameterDto request, bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"parameter:{request.ParId}:{request.Group}:{request.Country}:{request.Language}:{request.Code}:{request.Description}:{request.IsActive}:{request.SeqNo}";

        if (cache.TryGetValue(cacheKey, out List<Parameters.Models.Parameter>? cached))
            return cached!;

        var result = await inner.GetParameter(request, asNoTracking, cancellationToken);

        cache.Set(cacheKey, result);

        return result;
    }
}
