using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Reporting.Application.Services;
using Reporting.Data;

namespace Reporting.Infrastructure;

/// <summary>
/// DB-backed registry that joins <see cref="ReportDefinition"/> config rows (loaded from
/// <see cref="ReportingDbContext"/>) with the in-process <see cref="IReportDataProvider"/>
/// implementations registered in DI.
///
/// Config is cached per-node for ~60 seconds (absolute TTL) via <see cref="IMemoryCache"/>,
/// making report enable/disable and template changes take effect within one minute on all
/// nodes without a redeploy.
///
/// Scoped (not singleton) — providers depend on the scoped <see cref="ISqlConnectionFactory"/>;
/// promoting to singleton would introduce a captive-dependency error.
/// </summary>
internal sealed class ReportRegistry(
    IEnumerable<IReportDataProvider> providers,
    ReportingDbContext dbContext,
    IMemoryCache memoryCache) : IReportRegistry
{
    private const string CacheKey = "ReportRegistry:Definitions";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public ReportRegistration? TryGet(string reportTypeKey)
    {
        // Build provider lookup by key (cheap, in-process)
        var providerMap = providers.ToDictionary(
            p => p.ReportTypeKey,
            StringComparer.OrdinalIgnoreCase);

        // Load definitions from DB, cached for 60s per node
        var definitions = memoryCache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return dbContext.ReportDefinitions.AsNoTracking().ToList();
        })!;

        var defMap = definitions.ToDictionary(
            d => d.ReportTypeKey,
            StringComparer.OrdinalIgnoreCase);

        // Case 1: definition row exists — use its config
        if (defMap.TryGetValue(reportTypeKey, out var def))
        {
            // No provider registered for a known definition → cannot generate
            if (!providerMap.TryGetValue(reportTypeKey, out var provider))
                return null;

            return new ReportRegistration(
                reportTypeKey,
                def.TemplateId,
                provider,
                def.GenerationMode,
                def.IsEnabled);
        }

        // Case 2: provider exists but no DB definition (resilience / new provider not yet seeded)
        // Fall back to safe defaults so generation is not blocked by a missing config row.
        if (providerMap.TryGetValue(reportTypeKey, out var fallbackProvider))
        {
            return new ReportRegistration(
                reportTypeKey,
                reportTypeKey,
                fallbackProvider,
                ReportGenerationMode.Sync,
                IsEnabled: true);
        }

        // Case 3: completely unknown key
        return null;
    }
}
