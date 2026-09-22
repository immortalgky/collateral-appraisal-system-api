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
    /// KEEP IN SYNC with Application/Services/AppraisalValuationSummaryService.cs, which computes the
    /// same total in LINQ over tracked entities pre-save. The two cannot be collapsed (that path must
    /// see uncommitted values), so a change here needs the matching change there.
    /// </summary>
    internal static Task<decimal> ComputeAsync(ISqlConnectionFactory connectionFactory, Guid appraisalId)
    {
        // UNION ALL, never UNION: UNION would dedupe two properties that happen to carry an
        // identical amount, silently under-reporting the total.
        const string sql = """
            -- No rounding here: every derived figure below is already rounded to the nearest 1,000
            -- per property, so an all-derived appraisal still totals to a multiple of 1,000, while a
            -- coverage the appraiser keyed by hand reaches the book exactly as typed.
            SELECT ISNULL(SUM(x.InsuranceValue), 0)
            FROM (
                -- One row per building: the appraiser's keyed coverage wins, otherwise the
                -- depreciated value of its IsBuilding rows. LEFT JOIN so a building with no rows
                -- still contributes its keyed figure.
                SELECT COALESCE(bad.BuildingInsurancePriceOverride, ROUND(SUM(bdd.PriceAfterDepreciation), -3)) AS InsuranceValue
                FROM appraisal.BuildingAppraisalDetails bad
                JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
                LEFT JOIN appraisal.BuildingDepreciationDetails bdd
                       ON bdd.BuildingAppraisalDetailId = bad.Id
                      AND bdd.IsBuilding = 1
                WHERE ap.AppraisalId = @AppraisalId
                GROUP BY bad.Id, bad.BuildingInsurancePriceOverride

                UNION ALL

                -- Covers lease-agreement condo too: it writes the same CondoAppraisalDetails row.
                -- The derived figure rounds per property like the building side; a keyed one is taken as typed.
                SELECT COALESCE(cad.BuildingInsurancePriceOverride, ROUND(cad.BuildingInsurancePrice, -3), 0) AS InsuranceValue
                FROM appraisal.CondoAppraisalDetails cad
                JOIN appraisal.AppraisalProperties ap ON ap.Id = cad.AppraisalPropertyId
                WHERE ap.AppraisalId = @AppraisalId
            ) x
            """;

        return connectionFactory.QueryFirstOrDefaultAsync<decimal>(sql, new { AppraisalId = appraisalId });
    }
}
