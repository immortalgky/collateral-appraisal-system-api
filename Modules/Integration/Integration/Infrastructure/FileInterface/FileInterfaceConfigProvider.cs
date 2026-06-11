using Dapper;
using Integration.Contracts.FileInterface;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shared.Data;

namespace Integration.Infrastructure.FileInterface;

/// <summary>
/// Reads <see cref="FileInterfaceConfig"/> rows from <c>integration.FileInterfaceConfigs</c>
/// via Dapper with a 60-second in-memory cache per interface code.
/// Missing rows return null; inactive rows are also treated as missing.
/// </summary>
public class FileInterfaceConfigProvider(
    ISqlConnectionFactory connectionFactory,
    IMemoryCache cache,
    ILogger<FileInterfaceConfigProvider> logger) : IFileInterfaceConfigProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<FileInterfaceConfig?> GetAsync(string interfaceCode, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"fileiface:{interfaceCode}";

        if (cache.TryGetValue(cacheKey, out FileInterfaceConfig? cached))
            return cached;

        const string sql = """
            SELECT InterfaceCode, Direction, FileNamePrefix, FileNameDateFormat, FileExtension,
                   Directory, ProcessedDirectory, FilePattern, IsActive
            FROM integration.FileInterfaceConfigs
            WHERE InterfaceCode = @InterfaceCode AND IsActive = 1
            """;

        var connection = connectionFactory.GetOpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<RawRow>(sql, new { InterfaceCode = interfaceCode });

        FileInterfaceConfig? result = null;
        if (row is not null)
        {
            result = new FileInterfaceConfig(
                InterfaceCode: row.InterfaceCode,
                Direction: row.Direction,
                FileNamePrefix: row.FileNamePrefix,
                FileNameDateFormat: row.FileNameDateFormat,
                FileExtension: row.FileExtension,
                Directory: row.Directory,
                ProcessedDirectory: row.ProcessedDirectory,
                FilePattern: row.FilePattern,
                IsActive: row.IsActive);
        }
        else
        {
            logger.LogWarning("[FileInterfaceConfigProvider] No row found for interface code '{Code}'", interfaceCode);
        }

        cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    private sealed class RawRow
    {
        public string InterfaceCode { get; init; } = null!;
        public string Direction { get; init; } = null!;
        public string? FileNamePrefix { get; init; }
        public string? FileNameDateFormat { get; init; }
        public string? FileExtension { get; init; }
        public string? Directory { get; init; }
        public string? ProcessedDirectory { get; init; }
        public string? FilePattern { get; init; }
        public bool IsActive { get; init; }
    }
}
