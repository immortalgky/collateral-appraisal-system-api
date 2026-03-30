using Microsoft.Extensions.Caching.Memory;
using Parameter.Contracts.DocumentRequirements;

namespace Parameter.DocumentRequirements.Services;

public class CachedDocumentChecklistService(
    IDocumentChecklistService inner,
    IMemoryCache cache) : IDocumentChecklistService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public async Task<IReadOnlyList<DocumentChecklistItemDto>> GetApplicationRequirementsAsync(
        string? purposeCode, CancellationToken ct)
    {
        var cacheKey = $"docchecklist:app:{purposeCode}";

        return (await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await inner.GetApplicationRequirementsAsync(purposeCode, ct);
        }))!;
    }

    public async Task<IReadOnlyList<CollateralTypeDocumentGroupDto>> GetCollateralTypeRequirementsAsync(
        IEnumerable<string> collateralTypeCodes, string? purposeCode, CancellationToken ct)
    {
        var sortedCodes = collateralTypeCodes.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        var cacheKey = $"docchecklist:collateral:{string.Join(",", sortedCodes)}:{purposeCode}";

        return (await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await inner.GetCollateralTypeRequirementsAsync(sortedCodes, purposeCode, ct);
        }))!;
    }
}
