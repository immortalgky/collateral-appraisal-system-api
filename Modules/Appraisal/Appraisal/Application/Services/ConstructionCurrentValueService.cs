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
/// <param name="UnweightedPreviousPercent">
/// Plain average of each inspection's previous progress, read the way the CI screen records it:
/// summary mode from <c>SummaryPreviousProgressPct</c>, full detail from Σ(ProportionPct ×
/// PreviousProgressPct). Used only when there is no value base to weight by.
/// </param>
/// <param name="UnweightedCurrentPercent">The same for the current round's progress.</param>
public record ConstructionValueBreakdown(
    decimal LandValue,
    decimal CompletedBuildingValue,
    decimal InspectedTotalValue,
    decimal InspectedPreviousValue,
    decimal InspectedCurrentValue,
    decimal UnweightedPreviousPercent,
    decimal UnweightedCurrentPercent,
    bool HasOwnValueBase)
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
    public bool IsUnderConstruction =>
        HasOwnValueBase
            ? InspectedTotalValue > 0m && InspectedCurrentValue < InspectedTotalValue
            : UnweightedCurrentPercent < 100m;

    /// <summary>
    /// Construction progress as a value-weighted percentage of the inspected buildings, 0–100.
    /// A building worth ten times another moves this figure ten times as much — a plain average of
    /// per-building percentages would not. When the inspected buildings carry no value at all — a
    /// condo unit has no depreciation table to total — there is nothing to weight by, so this falls
    /// back to the plain average of the percentages the inspector actually entered.
    ///
    /// Reported to two decimal places, which is the precision every caller displays. The ratio is
    /// taken over whole-baht values (CA-614), so leaving it raw surfaces the rounding as noise —
    /// a building at a clean 15% came back as 14.999985000015. Rounding here keeps the artefact
    /// out of the API. IsUnderConstruction above is deliberately not derived from this property,
    /// so nothing decides "finished" off a rounded figure.
    /// </summary>
    public decimal ConstructionProgressPercent => AsReportedPercent(
        HasOwnValueBase && InspectedTotalValue > 0m
            ? InspectedCurrentValue / InspectedTotalValue * 100m
            : UnweightedCurrentPercent);

    /// <summary>Previous round's progress, on the same basis as <see cref="ConstructionProgressPercent"/>.</summary>
    public decimal PreviousProgressPercent => AsReportedPercent(
        HasOwnValueBase && InspectedTotalValue > 0m
            ? InspectedPreviousValue / InspectedTotalValue * 100m
            : UnweightedPreviousPercent);

    private static decimal AsReportedPercent(decimal value) =>
        Math.Round(Math.Clamp(value, 0m, 100m), 2, MidpointRounding.AwayFromZero);
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

        // No inspection anywhere on this appraisal → nothing is part-built. Test the row count,
        // not the value: CiAggregateSql is an ungrouped aggregate, so an appraisal with no inspection
        // still returns one all-zero row. Keying "nothing here" off TotalValue = 0 also swallowed the
        // inspections that legitimately carry no value base — a condo unit has no building
        // depreciation table for the CI screen to total, so its TotalValue is always 0.
        if (ci is null || ci.InspectionCount == 0)
            return null;

        var landValue = await connection.QueryFirstOrDefaultAsync<decimal>(
            new CommandDefinition(LandValueSql, p, cancellationToken: cancellationToken));

        var completedBuilding = await connection.QueryFirstOrDefaultAsync<decimal>(
            new CommandDefinition(CompletedBuildingValueSql, p, cancellationToken: cancellationToken));

        // A condo unit has no building depreciation table, so the CI screen has nothing to total and
        // every inspection on the appraisal carries TotalValue = 0. The appraised value is the same
        // "worth once finished" figure the depreciation table gives a house, so it stands in as the
        // 100% base and the entered percentages turn it into the previous and current figures.
        // Appraisal-level, so it can only substitute when NO inspection on the appraisal has a value
        // of its own — otherwise it would be attributing one number across several properties.
        if (ci.TotalValue > 0m)
        {
            return new ConstructionValueBreakdown(
                LandValue: landValue,
                CompletedBuildingValue: completedBuilding,
                InspectedTotalValue: ci.TotalValue,
                InspectedPreviousValue: ci.PreviousValue,
                InspectedCurrentValue: ci.CurrentValue,
                UnweightedPreviousPercent: ci.UnweightedPreviousPercent,
                UnweightedCurrentPercent: ci.UnweightedCurrentPercent,
                HasOwnValueBase: true);
        }

        var appraisedValue = await connection.QueryFirstOrDefaultAsync<decimal>(
            new CommandDefinition(AppraisedValueSql, p, cancellationToken: cancellationToken));

        // An inspection with no value base AND no appraised value has nothing to report. Keep the
        // original null so the caller's "nothing is part-built" path — and the regulatory writer's
        // CurrentValue ?? LatestAppraisalValue fallback — behave exactly as before.
        if (appraisedValue == 0m)
            return null;

        // Unscaled on purpose. A house is financed against how much of it is built, so its value
        // steps up with the percentage; a condo unit is not — the buyer is buying the finished unit
        // and nothing is drawn down per milestone. The percentage is still reported, it just does
        // not move the money.
        //
        // Land and completed buildings are dropped rather than added: AppraisedValue is the
        // WHOLE-appraisal figure and already contains them, so leaving them in would count them
        // twice in CurrentValue / CompleteValue / PreviousValue.
        return new ConstructionValueBreakdown(
            LandValue: 0m,
            CompletedBuildingValue: 0m,
            InspectedTotalValue: appraisedValue,
            InspectedPreviousValue: appraisedValue,
            InspectedCurrentValue: appraisedValue,
            UnweightedPreviousPercent: ci.UnweightedPreviousPercent,
            UnweightedCurrentPercent: ci.UnweightedCurrentPercent,
            HasOwnValueBase: false);
    }

    /// <summary>
    /// The appraisal's own "worth once finished" figure, used as the 100% base for an inspection that
    /// has no value of its own. Same column the Decision Summary and the regulatory export read.
    /// </summary>
    private const string AppraisedValueSql = """
        SELECT ISNULL(MAX(va.AppraisedValue), 0)
        FROM appraisal.ValuationAnalyses va
        WHERE va.AppraisalId = @AppraisalId
        """;

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
    ///
    /// Each inspection's contribution is rounded to whole baht (CA-614). ROUND rounds halves away
    /// from zero, matching Appraisal.Domain.Appraisals.ConstructionMoney, which applies the same
    /// rule when full-detail values are persisted. Two other places repeat this aggregate and have
    /// to keep the same rounding: AppraisalSummaryConstructionDataProvider in the Reporting module
    /// and collateral.vw_RegulatoryExport.
    /// </summary>
    private const string CiAggregateSql = """
        SELECT
            ISNULL(SUM(ci.TotalValue), 0) AS TotalValue,
            ISNULL(SUM(ROUND(
                CASE WHEN ci.IsFullDetail = 0
                     THEN ci.TotalValue * ISNULL(ci.SummaryPreviousProgressPct, 0) / 100.0
                     ELSE ISNULL(wd.PreviousPropertyValueSum, 0)
                END
            , 0)), 0) AS PreviousValue,
            ISNULL(SUM(ROUND(
                CASE WHEN ci.IsFullDetail = 0
                     THEN ci.TotalValue * ISNULL(ci.SummaryCurrentProgressPct, 0) / 100.0
                     ELSE ISNULL(wd.CurrentPropertyValueSum, 0)
                END
            , 0)), 0) AS CurrentValue,
            COUNT(*) AS InspectionCount,
            -- The progress the inspector actually entered, read per the mode flag exactly as
            -- ConstructionInspection.OverallCurrentProgressPercent does: summary mode keeps its own
            -- percentage, full detail sums the weighted work rows. Averaged rather than weighted
            -- because these are only consulted when there is no value to weight by.
            ISNULL(AVG(
                CASE WHEN ci.IsFullDetail = 0
                     THEN ISNULL(ci.SummaryPreviousProgressPct, 0)
                     ELSE ISNULL(wd.PreviousProportionPctSum, 0)
                END
            ), 0) AS UnweightedPreviousPercent,
            ISNULL(AVG(
                CASE WHEN ci.IsFullDetail = 0
                     THEN ISNULL(ci.SummaryCurrentProgressPct, 0)
                     ELSE ISNULL(wd.CurrentProportionPctSum, 0)
                END
            ), 0) AS UnweightedCurrentPercent
        FROM appraisal.ConstructionInspections ci
        JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
        LEFT JOIN (
            SELECT ConstructionInspectionId,
                   SUM(PreviousPropertyValue) AS PreviousPropertyValueSum,
                   SUM(CurrentPropertyValue)  AS CurrentPropertyValueSum,
                   -- No PreviousProportionPct column exists; it is the same product the server
                   -- computes into CurrentProportionPct, taken against the previous round.
                   SUM(ProportionPct * PreviousProgressPct / 100.0) AS PreviousProportionPctSum,
                   SUM(CurrentProportionPct)                        AS CurrentProportionPctSum
            FROM appraisal.ConstructionWorkDetails
            GROUP BY ConstructionInspectionId
        ) wd ON wd.ConstructionInspectionId = ci.Id
        WHERE ap.AppraisalId = @AppraisalId
        """;

    private sealed record CiAggregate(
        decimal TotalValue,
        decimal PreviousValue,
        decimal CurrentValue,
        int InspectionCount,
        decimal UnweightedPreviousPercent,
        decimal UnweightedCurrentPercent);
}
