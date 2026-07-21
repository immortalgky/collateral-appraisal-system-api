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

    private async Task<decimal> ResolveProjectOrDefaultAsync(Guid appraisalId, CancellationToken ct)
    {
        const string projectForceSaleSql = """
            SELECT ppa.ForceSalePercentage
            FROM appraisal.Projects p
            JOIN appraisal.ProjectPricingAssumptions ppa ON ppa.ProjectId = p.Id
            WHERE p.AppraisalId = @AppraisalId
            """;

        var projectRate = await connectionFactory.QueryFirstOrDefaultAsync<decimal?>(
            projectForceSaleSql, new { AppraisalId = appraisalId });

        return projectRate ?? await configReader.GetDecimalAsync("ForceSaleRateDefaultPct", FallbackRate, ct);
    }
}
