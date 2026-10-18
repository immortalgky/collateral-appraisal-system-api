using Shared.Configuration;
using Shared.Data;
using Shared.Pagination;

namespace Appraisal.Application.Services;

/// <summary>
/// Single source of truth for resolving the force-sale rate for an appraisal. Used by both the
/// Decision Summary read (<c>GetDecisionSummaryQueryHandler</c>) and write
/// (<c>SaveDecisionSummaryCommandHandler</c>) paths so the resolution chain lives in one place.
///
/// Resolution order:
///   1. Appraisal-level override (<see cref="Domain.Appraisals.ValuationAnalysis.ForceSaleRate"/>).
///   2. Block appraisal only: <c>ProjectPricingAssumptions.ForceSalePercentage</c> for the
///      project attached to this appraisal (no-op — falls through — for non-block appraisals).
///   3. System-wide default (SystemConfiguration key "ForceSaleRateDefaultPct").
///   4. Hardcoded 70m last resort.
///
/// A resolved value outside (0, 100] is treated as a config error (e.g. an admin entering "0.7"
/// instead of "70") and falls back to 70m, with a warning logged.
/// </summary>
public class ForceSaleRateResolver(
    ISqlConnectionFactory connectionFactory,
    ISystemConfigurationReader configReader,
    ILogger<ForceSaleRateResolver> logger)
{
    private const decimal FallbackRate = 70m;

    /// <summary>
    /// System-wide default alone (step 3 of the chain), for callers that apply the project's own value
    /// themselves: within 0..100, else 70 (0 is allowed here, unlike <see cref="ResolveAsync"/>).
    /// </summary>
    public async Task<decimal> GetSystemDefaultAsync(CancellationToken ct) =>
        Project.SystemDefaultOrFallback(await configReader.GetDecimalAsync("ForceSaleRateDefaultPct", FallbackRate, ct));

    public async Task<decimal> ResolveAsync(Guid appraisalId, decimal? overrideRate, CancellationToken ct)
    {
        var rate = overrideRate ?? await ResolveProjectOrDefaultAsync(appraisalId, ct);

        if (rate is <= 0 or > 100)
        {
            logger.LogWarning(
                "Resolved force-sale rate {Rate} for AppraisalId {AppraisalId} is outside the valid " +
                "(0, 100] range — falling back to {FallbackRate}.",
                rate, appraisalId, FallbackRate);
            return FallbackRate;
        }

        return rate;
    }

    /// <summary>
    /// The % a block's unit prices are calculated with: the project's own when within 0..100 (0% honoured),
    /// else the system default, else 70. The one place that reads the project's % for the Decision Summary
    /// paths (Calculate works on the aggregate it has already loaded).
    /// </summary>
    public async Task<decimal> ResolveBlockEffectivePercentAsync(Guid appraisalId, CancellationToken ct) =>
        Project.ResolveForceSalePercentage(
            await ReadProjectRateAsync(appraisalId), await GetSystemDefaultAsync(ct));

    private Task<decimal?> ReadProjectRateAsync(Guid appraisalId)
    {
        const string projectForceSaleSql = """
            SELECT ppa.ForceSalePercentage
            FROM appraisal.Projects p
            JOIN appraisal.ProjectPricingAssumptions ppa ON ppa.ProjectId = p.Id
            WHERE p.AppraisalId = @AppraisalId
            """;

        return connectionFactory.QueryFirstOrDefaultAsync<decimal?>(
            projectForceSaleSql, new { AppraisalId = appraisalId });
    }

    private async Task<decimal> ResolveProjectOrDefaultAsync(Guid appraisalId, CancellationToken ct) =>
        await ReadProjectRateAsync(appraisalId)
        ?? await configReader.GetDecimalAsync("ForceSaleRateDefaultPct", FallbackRate, ct);
}
