namespace Shared.Configuration;

/// <summary>
/// Cross-module reader for system-wide configuration entries stored in the Common module.
/// Values are cached for a short TTL; use this for infrequently-changing admin settings.
/// </summary>
public interface ISystemConfigurationReader
{
    Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default);
    Task<string?> GetStringAsync(string key, string? defaultValue = null, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken ct = default);
    Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken ct = default);
}
