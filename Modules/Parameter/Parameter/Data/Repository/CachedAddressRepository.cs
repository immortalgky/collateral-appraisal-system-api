using Microsoft.Extensions.Caching.Memory;

namespace Parameter.Data.Repository;

public class CachedAddressRepository(IAddressRepository inner, IMemoryCache cache) : IAddressRepository
{
    // Kept short deliberately: this is an in-process cache, so on a multi-node deployment each node
    // holds and expires its own copy and a cache.Remove would only clear the node that served the
    // request. The admin CRUD in Addresses/Features/AdminAddresses does not invalidate this cache,
    // so the TTL is what bounds how long an added province stays invisible.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

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
