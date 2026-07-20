using Shared.Data;

namespace Appraisal.Application.Features.DecisionSummary;

internal static class BuildingInsuranceCalculator
{
    /// <summary>
    /// Appraisal-level insurance total for NON-BLOCK appraisals: every insurable structure summed.
    /// Buildings contribute their depreciated structure value; condos contribute the rate-derived
    /// coverage amount (RatePerSqm × UsableArea) stored by CondoFireInsuranceCalculator. Land is
    /// deliberately excluded. Block appraisals never reach here — SaveDecisionSummaryCommandHandler
    /// short-circuits to SUM(ProjectUnitPrices.CoverageAmount) when a Project row exists.
    ///
    /// KEEP IN SYNC with Application/EventHandlers/AppraisalFinalValuesChangedEventHandler.cs, which
    /// computes the same total in LINQ over tracked entities pre-save. The two cannot be collapsed
    /// (that handler must see uncommitted values), so a change here needs the matching change there.
    /// </summary>
    internal static Task<decimal> ComputeAsync(ISqlConnectionFactory connectionFactory, Guid appraisalId)
    {
        // UNION ALL, never UNION: UNION would dedupe two properties that happen to carry an
        // identical amount, silently under-reporting the total.
        const string sql = """
            SELECT ISNULL(SUM(x.InsuranceValue), 0)
            FROM (
                SELECT bdd.PriceAfterDepreciation AS InsuranceValue
                FROM appraisal.BuildingDepreciationDetails bdd
                JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bdd.BuildingAppraisalDetailId
                JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
                WHERE ap.AppraisalId = @AppraisalId
                  AND bdd.IsBuilding = 1

                UNION ALL

                -- Covers lease-agreement condo too: it writes the same CondoAppraisalDetails row.
                SELECT ISNULL(cad.BuildingInsurancePrice, 0) AS InsuranceValue
                FROM appraisal.CondoAppraisalDetails cad
                JOIN appraisal.AppraisalProperties ap ON ap.Id = cad.AppraisalPropertyId
                WHERE ap.AppraisalId = @AppraisalId
            ) x
            """;

        return connectionFactory.QueryFirstOrDefaultAsync<decimal>(sql, new { AppraisalId = appraisalId });
    }
}
