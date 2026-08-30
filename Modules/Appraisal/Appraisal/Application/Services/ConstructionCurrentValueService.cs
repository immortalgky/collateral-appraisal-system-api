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
    decimal WeightedPreviousPercent,
    decimal WeightedCurrentPercent,
    bool HasOwnValueBase)
{
    /// <summary>Value as it stands today, with part-built buildings counted at their progress.</summary>
    public decimal CurrentValue => LandValue + CompletedBuildingValue + InspectedCurrentValue;

    /// <summary>Value once construction finishes — should reconcile with the appraised value.</summary>
    public decimal CompleteValue => LandValue + CompletedBuildingValue + InspectedTotalValue;

    /// <summary>Value at the previous inspection round.</summary>
    public decimal PreviousValue => LandValue + CompletedBuildingValue + InspectedPreviousValue;

    /// <summary>
    /// Construction progress across the inspected buildings, 0–100.
    ///
    /// Read off the percentages the inspector entered — per building, the weighted work rows
    /// (Σ ProportionPct × CurrentProgressPct / 100) in full-detail mode or SummaryCurrentProgressPct
    /// in summary mode — and combined across buildings in proportion to what each is worth, so a
    /// building worth ten times another moves the figure ten times as much.
    ///
    /// <b>Deliberately not InspectedCurrentValue / InspectedTotalValue.</b> That division is
    /// algebraically the same figure, but its inputs are money, and money is rounded to whole baht
    /// (CA-614): the rounded parts no longer sum to the rounded whole, so a finished building came
    /// out a baht short of its own 100% figure and reported as still under construction. The
    /// percentages carry no such problem — they are stored as decimal(7,4) and nothing rounds them.
    /// TotalValue appears here only as a weight, and a weight is a ratio: 9:1 stays 9:1 whether or
    /// not the amounts carry satang.
    ///
    /// With no value base to weight by — a condo unit has no depreciation table to total, so every
    /// inspection on the appraisal carries TotalValue = 0 — this falls back to the plain average.
    ///
    /// Reported to two decimal places, the precision every caller displays.
    /// </summary>
    public decimal ConstructionProgressPercent => AsReportedPercent(RawCurrentPercent);

    /// <summary>Previous round's progress, on the same basis as <see cref="ConstructionProgressPercent"/>.</summary>
    public decimal PreviousProgressPercent => AsReportedPercent(RawPreviousPercent);

    /// <summary>
    /// True while the inspected buildings are short of complete.
    ///
    /// Compares the unrounded percentage, not the rounded one: a split that leaves the work at
    /// 99.996% is not finished, and rounding it to 100.00 for display must not decide otherwise.
    ///
    /// Nothing validates that ProportionPct sums to 100, so an inspection whose split is short
    /// reports as unfinished even at full progress on every item. That is long-standing behaviour,
    /// unchanged here.
    /// </summary>
    public bool IsUnderConstruction => RawCurrentPercent < 100m;

    private decimal RawCurrentPercent =>
        HasOwnValueBase ? WeightedCurrentPercent : UnweightedCurrentPercent;

    private decimal RawPreviousPercent =>
        HasOwnValueBase ? WeightedPreviousPercent : UnweightedPreviousPercent;

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
                WeightedPreviousPercent: ci.WeightedPreviousPercent,
                WeightedCurrentPercent: ci.WeightedCurrentPercent,
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
            WeightedPreviousPercent: ci.WeightedPreviousPercent,
            WeightedCurrentPercent: ci.WeightedCurrentPercent,
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
    /// Each inspection's money contribution is rounded to whole baht (CA-614). ROUND rounds halves
    /// away from zero, matching Appraisal.Domain.Appraisals.ConstructionMoney, which applies the
    /// same rule when full-detail values are persisted.
    ///
    /// The money columns answer "how much". Progress is answered separately by the Weighted*Percent
    /// columns, which never touch money. Two other places repeat this aggregate and have to keep
    /// both rules — the rounding and the percentage source — in step:
    /// AppraisalSummaryConstructionDataProvider in the Reporting module, and
    /// collateral.vw_RegulatoryExport.
    /// </summary>
    private const string CiAggregateSql = """
        SELECT
            ISNULL(SUM(v.TotalValue), 0)                 AS TotalValue,
            ISNULL(SUM(ROUND(v.PreviousValue, 0)), 0)    AS PreviousValue,
            ISNULL(SUM(ROUND(e.CurrentValue, 0)), 0)     AS CurrentValue,
            COUNT(*)                                     AS InspectionCount,
            -- Plain averages, consulted only when there is no value to weight by.
            ISNULL(AVG(v.PreviousPct), 0)                AS UnweightedPreviousPercent,
            ISNULL(AVG(e.CurrentPct), 0)                 AS UnweightedCurrentPercent,
            -- Per-building progress weighted across buildings by what each is worth. This is what
            -- decides "finished" and what the reports print — deliberately NOT CurrentValue /
            -- TotalValue. Money is rounded to whole baht (CA-614), so the rounded parts no longer
            -- sum to the rounded whole and a finished building came out a baht short of its own
            -- 100% figure. These percentages are decimal(7,4) and nothing rounds them; TotalValue
            -- is only a weight here, and a weight is a ratio.
            CASE WHEN SUM(v.TotalValue) > 0
                 THEN SUM(v.TotalValue * v.PreviousPct) / SUM(v.TotalValue)
                 ELSE 0 END                              AS WeightedPreviousPercent,
            CASE WHEN SUM(v.TotalValue) > 0
                 THEN SUM(v.TotalValue * e.CurrentPct) / SUM(v.TotalValue)
                 ELSE 0 END                              AS WeightedCurrentPercent
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
        -- One row per inspection, read per the mode flag: summary mode keeps its own percentage,
        -- full detail sums the weighted work rows. Money for summary mode is derived from the
        -- percentage rather than read from SummaryPreviousValue / SummaryCurrentValue — see the
        -- interface remarks for why those columns cannot be trusted.
        CROSS APPLY (
            SELECT
                ci.TotalValue,
                CASE WHEN ci.IsFullDetail = 0 THEN ISNULL(ci.SummaryPreviousProgressPct, 0)
                     ELSE ISNULL(wd.PreviousProportionPctSum, 0) END AS PreviousPct,
                CASE WHEN ci.IsFullDetail = 0 THEN ISNULL(ci.SummaryCurrentProgressPct, 0)
                     ELSE ISNULL(wd.CurrentProportionPctSum, 0) END  AS CurrentPctRaw,
                CASE WHEN ci.IsFullDetail = 0
                     THEN ci.TotalValue * ISNULL(ci.SummaryPreviousProgressPct, 0) / 100.0
                     ELSE ISNULL(wd.PreviousPropertyValueSum, 0) END AS PreviousValue,
                CASE WHEN ci.IsFullDetail = 0
                     THEN ci.TotalValue * ISNULL(ci.SummaryCurrentProgressPct, 0) / 100.0
                     ELSE ISNULL(wd.CurrentPropertyValueSum, 0) END  AS CurrentValueRaw
        ) v
        -- A round the inspector has not filled in yet carries 0% current against a non-zero
        -- previous, because CopyForNextInspection resets the current figures for them to enter.
        -- Reporting that as 0% would say the building went backwards — the work done in earlier
        -- rounds is still standing. Until something is entered, the round stands where the last
        -- one left it.
        CROSS APPLY (
            SELECT
                CASE WHEN v.CurrentPctRaw = 0 AND v.PreviousPct > 0
                     THEN v.PreviousPct ELSE v.CurrentPctRaw END   AS CurrentPct,
                CASE WHEN v.CurrentPctRaw = 0 AND v.PreviousPct > 0
                     THEN v.PreviousValue ELSE v.CurrentValueRaw END AS CurrentValue
        ) e
        WHERE ap.AppraisalId = @AppraisalId
        """;

    private sealed record CiAggregate(
        decimal TotalValue,
        decimal PreviousValue,
        decimal CurrentValue,
        int InspectionCount,
        decimal UnweightedPreviousPercent,
        decimal UnweightedCurrentPercent,
        decimal WeightedPreviousPercent,
        decimal WeightedCurrentPercent);
}
