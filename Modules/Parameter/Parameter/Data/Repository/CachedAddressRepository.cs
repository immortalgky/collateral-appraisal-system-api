using Microsoft.Extensions.Caching.Memory;

namespace Parameter.Data.Repository;

public class CachedAddressRepository(IAddressRepository inner, IMemoryCache cache) : IAddressRepository
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public async Task<List<AddressDto>> GetTitleAddressesAsync(CancellationToken ct = default)
    {
        const string cacheKey = "addresses:title";

        if (cache.TryGetValue(cacheKey, out List<AddressDto>? cached))
            return cached!;

        var result = await inner.GetTitleAddressesAsync(ct);

        cache.Set(cacheKey, result, CacheDuration);

        return result;
    }

    public async Task<List<AddressDto>> GetDopaAddressesAsync(CancellationToken ct = default)
    {
        const string cacheKey = "addresses:dopa";

        if (cache.TryGetValue(cacheKey, out List<AddressDto>? cached))
            return cached!;

        var result = await inner.GetDopaAddressesAsync(ct);

        cache.Set(cacheKey, result, CacheDuration);

        return result;
    }
}
