using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shared.Configuration;

namespace Common.Infrastructure.Configuration;

/// <summary>
/// Reads SystemConfiguration rows from CommonDbContext with a 60-second in-memory cache per key.
/// Missing or inactive entries return the caller-supplied default value.
/// </summary>
public class SystemConfigurationReader(
    CommonDbContext dbContext,
    IMemoryCache cache,
    ILogger<SystemConfigurationReader> logger)
    : ISystemConfigurationReader
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(key, ct);
        if (raw is null) return defaultValue;

        if (int.TryParse(raw, out var parsed)) return parsed;

        logger.LogWarning("SystemConfiguration key '{Key}' has value '{Value}' that cannot be parsed as int. Returning default {Default}.", key, raw, defaultValue);
        return defaultValue;
    }

    public async Task<string?> GetStringAsync(string key, string? defaultValue = null, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(key, ct);
        return raw ?? defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(key, ct);
        if (raw is null) return defaultValue;

        if (bool.TryParse(raw, out var parsed)) return parsed;

        logger.LogWarning("SystemConfiguration key '{Key}' has value '{Value}' that cannot be parsed as bool. Returning default {Default}.", key, raw, defaultValue);
        return defaultValue;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(key, ct);
        if (raw is null) return defaultValue;

        if (decimal.TryParse(raw, out var parsed)) return parsed;

        logger.LogWarning("SystemConfiguration key '{Key}' has value '{Value}' that cannot be parsed as decimal. Returning default {Default}.", key, raw, defaultValue);
        return defaultValue;
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private async Task<string?> GetRawAsync(string key, CancellationToken ct)
    {
        var cacheKey = $"syscfg:{key}";

        if (cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        var entry = await dbContext.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.Key == key && c.IsActive)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct);

        cache.Set(cacheKey, entry, CacheTtl);

        return entry;
    }
}
