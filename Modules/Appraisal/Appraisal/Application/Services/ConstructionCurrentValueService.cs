using Dapper;
using Shared.Data;

namespace Appraisal.Application.Services;

/// <summary>
/// The single implementation of "what is this appraisal worth right now, part-built".
///
/// One calculation, two consumers — the Decision Summary construction card and the
/// <c>AppraisalForCollateralResult</c> contract that feeds the Collateral module (and from there the
/// regulatory export's Appraisal-Value-as-Completed field). Keeping it here means the screen and the
/// outbound regulatory file can never drift apart.
///
/// <code>
/// CurrentValue = LandValue + CompletedBuildingValue + ConstructionCurrentValue
/// CompleteValue = LandValue + CompletedBuildingValue + ConstructionTotalValue
/// </code>
///
/// <b>Summary-mode values are derived from the percent, not read from the stored value.</b>
/// <c>ConstructionInspections.SummaryCurrentValue</c> is unusable: the CI screen computes the figure
/// in a <c>useMemo</c> and displays it, but never writes it back into the form, so the payload sends
/// the default (0) — the screen shows one number and the database stores another. The percent
/// (<c>SummaryCurrentProgressPct</c>) is bound to a real form input and does persist, so it is the
/// trustworthy input. Full-detail mode is unaffected: the server already computes
/// <c>ConstructionWorkDetail.CurrentPropertyValue</c> from the percentages on save.
/// </summary>
public interface IConstructionCurrentValueService
{
    /// <summary>
    /// Returns null when the appraisal has no construction inspection at all — nothing is part-built,
    /// so there is no "current" value distinct from the appraised value.
    /// </summary>
    Task<ConstructionValueBreakdown?> GetAsync(Guid appraisalId, CancellationToken cancellationToken = default);
}

/// <param name="LandValue">Σ PricingFinalValues.LandValue over the appraisal's property groups.</param>
/// <param name="CompletedBuildingValue">
/// Σ PriceAfterDepreciation for building properties with NO construction inspection — i.e. already
/// finished before any inspection round, so they count at full value.
/// </param>
/// <param name="InspectedTotalValue">Σ ConstructionInspections.TotalValue — the part-built buildings at 100%.</param>
/// <param name="InspectedPreviousValue">Those same buildings at the previous round's progress.</param>
/// <param name="InspectedCurrentValue">Those same buildings at the current round's progress.</param>
public record ConstructionValueBreakdown(
    decimal LandValue,
    decimal CompletedBuildingValue,
    decimal InspectedTotalValue,
    decimal InspectedPreviousValue,
    decimal InspectedCurrentValue)
{
    /// <summary>Value as it stands today, with part-built buildings counted at their progress.</summary>
    public decimal CurrentValue => LandValue + CompletedBuildingValue + InspectedCurrentValue;

    /// <summary>Value once construction finishes — should reconcile with the appraised value.</summary>
    public decimal CompleteValue => LandValue + CompletedBuildingValue + InspectedTotalValue;

    /// <summary>Value at the previous inspection round.</summary>
    public decimal PreviousValue => LandValue + CompletedBuildingValue + InspectedPreviousValue;

    /// <summary>
    /// True while any inspected building is short of its finished value.
    ///
    /// Weighted by value across EVERY inspected building, unlike the older
    /// <c>primaryProperty.ConstructionInspection.OverallCurrentProgressPercent &lt; 100</c> rule, which
    /// read one property and silently ignored the rest of a multi-building appraisal.
    /// </summary>
    public bool IsUnderConstruction => InspectedTotalValue > 0m && InspectedCurrentValue < InspectedTotalValue;

    /// <summary>
    /// Construction progress as a value-weighted percentage of the inspected buildings, 0–100.
    /// A building worth ten times another moves this figure ten times as much — a plain average of
    /// per-building percentages would not. Returns 100 when nothing is under inspection, matching the
    /// regulatory export's "completed" case.
    /// </summary>
    public decimal ConstructionProgressPercent =>
        InspectedTotalValue > 0m
            ? Math.Clamp(InspectedCurrentValue / InspectedTotalValue * 100m, 0m, 100m)
            : 100m;
}

public class ConstructionCurrentValueService(ISqlConnectionFactory connectionFactory)
    : IConstructionCurrentValueService
{
    public async Task<ConstructionValueBreakdown?> GetAsync(
        Guid appraisalId,
        CancellationToken cancellationToken = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // GetOpenConnection + CommandDefinition rather than the ISqlConnectionFactory extension
        // methods, because those take no CancellationToken (see DapperPaginationExtensions).
        var connection = connectionFactory.GetOpenConnection();

        var ci = await connection.QueryFirstOrDefaultAsync<CiAggregate>(
            new CommandDefinition(CiAggregateSql, p, cancellationToken: cancellationToken));

        // No inspection anywhere on this appraisal → nothing is part-built.
        if (ci is null || ci.TotalValue == 0m)
            return null;

        var landValue = await connection.QueryFirstOrDefaultAsync<decimal>(
            new CommandDefinition(LandValueSql, p, cancellationToken: cancellationToken));

        var completedBuilding = await connection.QueryFirstOrDefaultAsync<decimal>(
            new CommandDefinition(CompletedBuildingValueSql, p, cancellationToken: cancellationToken));

        return new ConstructionValueBreakdown(
            LandValue: landValue,
            CompletedBuildingValue: completedBuilding,
            InspectedTotalValue: ci.TotalValue,
            InspectedPreviousValue: ci.PreviousValue,
            InspectedCurrentValue: ci.CurrentValue);
    }

    /// <summary>
    /// Land component. NOTE: PricingFinalValues.LandValue is only written for per-unit-rate methods
    /// (PerSqWa / PerSqm); a whole-unit lumpsum method carries no land rate and leaves it NULL by
    /// design, so this can legitimately be 0. Historical rows saved before the server-side derivation
    /// shipped also need Database/Scripts/Maintenance/BackfillPricingFinalValueLandArea.sql.
    /// </summary>
    private const string LandValueSql = """
        SELECT ISNULL(SUM(pfv.LandValue), 0)
        FROM appraisal.PricingFinalValues pfv
        JOIN appraisal.PricingAnalysisMethods pam ON pam.Id = pfv.PricingMethodId
        JOIN appraisal.PricingAnalysisApproaches paa ON paa.Id = pam.ApproachId
        JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId AND pa.SubjectType = 0
        JOIN appraisal.PropertyGroups pg ON pg.Id = pa.AnchorId
        WHERE pg.AppraisalId = @AppraisalId
        """;

    /// <summary>Buildings with no inspection — finished, so they count at full depreciated value.</summary>
    private const string CompletedBuildingValueSql = """
        SELECT ISNULL(SUM(bdd.PriceAfterDepreciation), 0)
        FROM appraisal.BuildingDepreciationDetails bdd
        JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bdd.BuildingAppraisalDetailId
        JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
        WHERE ap.AppraisalId = @AppraisalId
          AND NOT EXISTS (
              SELECT 1 FROM appraisal.ConstructionInspections ci
              WHERE ci.AppraisalPropertyId = ap.Id
          )
        """;

    /// <summary>
    /// Part-built buildings, at 100% / previous progress / current progress.
    ///
    /// Summary mode multiplies TotalValue by the stored percent rather than reading
    /// SummaryPreviousValue / SummaryCurrentValue — see the interface remarks for why those columns
    /// cannot be trusted. Full-detail mode sums the work details, which the server computes on save.
    /// </summary>
    private const string CiAggregateSql = """
        SELECT
            ISNULL(SUM(ci.TotalValue), 0) AS TotalValue,
            ISNULL(SUM(
                CASE WHEN ci.IsFullDetail = 0
                     THEN ci.TotalValue * ISNULL(ci.SummaryPreviousProgressPct, 0) / 100.0
                     ELSE ISNULL(wd.PreviousPropertyValueSum, 0)
                END
            ), 0) AS PreviousValue,
            ISNULL(SUM(
                CASE WHEN ci.IsFullDetail = 0
                     THEN ci.TotalValue * ISNULL(ci.SummaryCurrentProgressPct, 0) / 100.0
                     ELSE ISNULL(wd.CurrentPropertyValueSum, 0)
                END
            ), 0) AS CurrentValue
        FROM appraisal.ConstructionInspections ci
        JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
        LEFT JOIN (
            SELECT ConstructionInspectionId,
                   SUM(PreviousPropertyValue) AS PreviousPropertyValueSum,
                   SUM(CurrentPropertyValue)  AS CurrentPropertyValueSum
            FROM appraisal.ConstructionWorkDetails
            GROUP BY ConstructionInspectionId
        ) wd ON wd.ConstructionInspectionId = ci.Id
        WHERE ap.AppraisalId = @AppraisalId
        """;

    private sealed record CiAggregate(decimal TotalValue, decimal PreviousValue, decimal CurrentValue);
}
