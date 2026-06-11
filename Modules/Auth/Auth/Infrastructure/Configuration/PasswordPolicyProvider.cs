using Auth.Domain.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace Auth.Infrastructure.Configuration;

/// <summary>
/// Reads the single password-policy row with a 60-second in-memory cache so the validator and
/// policy endpoint don't hit the database on every password attempt, while still picking up admin
/// edits within a minute (the update handler also invalidates the cache eagerly).
/// </summary>
public interface IPasswordPolicyProvider
{
    Task<PasswordPolicy> GetAsync(CancellationToken ct = default);
    void Invalidate();
}

public class PasswordPolicyProvider(AuthDbContext dbContext, IMemoryCache cache)
    : IPasswordPolicyProvider
{
    private const string CacheKey = "pwpolicy";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<PasswordPolicy> GetAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out PasswordPolicy? cached) && cached is not null)
            return cached;

        var policy = await dbContext.PasswordPolicy.AsNoTracking().FirstOrDefaultAsync(ct)
                     ?? PasswordPolicy.CreateDefault();

        cache.Set(CacheKey, policy, CacheTtl);
        return policy;
    }

    public void Invalidate() => cache.Remove(CacheKey);
}
