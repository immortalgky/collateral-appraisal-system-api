using Dapper;
using Shared.Data;

namespace Appraisal.Application.Services;

/// <summary>
/// The single implementation of "what is this appraisal worth right now, part-built".
///
/// One calculation, two consumers — the Decision Summary construction card and the
/// <c>AppraisalForCollateralResult</c> contract that feeds the Collateral module, where it is frozen
/// onto the engagement and read back by the reappraisal queue. The regulatory export does NOT read
/// this service: <c>collateral.vw_RegulatoryExport</c> derives its own current value from
/// ConstructionInspections and means something narrower by it (the part-built buildings alone).
///
/// <code>
/// CompleteValue = AppraisedValue of the inspected groups, else Land + CompletedBuilding + InspectedTotal
/// CurrentValue  = 100%? CompleteValue : min(Land + CompletedBuilding + InspectedCurrent, CompleteValue)
/// PreviousValue = the same rule, on the previous round's progress
/// </code>
///
/// <b>Everything is scoped to the groups holding an inspected property</b> — see
/// <c>CiGroupsSql</c>. An appraisal's machinery, bare plots and uninspected condos are separate
/// collateral and have no place in a drawdown computed from this book's progress percentage.
///
/// <b>The completed value is the price of record, not the sum of its parts.</b> The components can
/// total something else — the Cost approach prices a building through its Building Cost method while
/// the inspection carries the depreciation total — and when they disagree the priced figure wins, so
/// this card, the construction summary book and the pricing screen cannot print three answers.
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

/// <param name="LandValue">Σ PricingFinalValues.LandValue over the inspected groups.</param>
/// <param name="CompletedBuildingValue">
/// Per building with NO construction inspection — already finished before any round, so it counts at
/// full value — the appraiser's own Building Cost Value where they keyed one, else the depreciated
/// sum rounded to the nearest 1,000. Summed over the inspected groups.
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
/// <param name="AppraisedValue">
/// Σ the appraised value of the inspected groups — the price of record, what the pricing screen
/// settled on. 0 when those groups carry no price yet.
/// </param>
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
    bool HasOwnValueBase,
    decimal AppraisedValue = 0m)
{
    /// <summary>
    /// Value once construction finishes: the appraised value of the inspected groups, falling back
    /// to the components when those groups carry no price yet.
    ///
    /// The two can differ — the Cost approach prices the building through its Building Cost method
    /// while the inspection carries the building's own depreciation total, and adjusting one does
    /// not move the other. Reporting the appraised value means this card, the construction summary
    /// book and the price of record all agree.
    ///
    /// KNOWN CONSEQUENCE: the land and building columns beside this row no longer have to add up to
    /// it. That is the deliberate trade — the alternative is a card that disagrees with the
    /// appraisal it summarises.
    /// </summary>
    public decimal CompleteValue => AppraisedValue > 0m
        ? AppraisedValue
        : LandValue + CompletedBuildingValue + InspectedTotalValue;

    /// <summary>
    /// Value as it stands today, with part-built buildings counted at their progress. At 100% there
    /// is nothing left to build, so it is the completed value itself — reporting less would say a
    /// finished property is worth less than its own appraisal. Below 100% it is capped there for the
    /// same reason: the two are computed from different bases, and when the appraiser prices below
    /// what the inspection's totals imply, a part-built figure could otherwise print higher than the
    /// finished one. <see cref="IsUnderConstruction"/> reads the unrounded progress, so a split
    /// leaving the work at 99.995% is still unfinished however it displays.
    /// </summary>
    public decimal CurrentValue => IsUnderConstruction
        ? Math.Min(LandValue + CompletedBuildingValue + InspectedCurrentValue, CompleteValue)
        : CompleteValue;

    /// <summary>
    /// Value at the previous inspection round, on exactly the same rule as <see cref="CurrentValue"/>:
    /// the completed value once that round reached 100%, the capped components below it.
    ///
    /// The symmetry is what keeps the delta rows honest. Capping without lifting meant a re-inspection
    /// of an already-finished building reported the previous round at the components and the current
    /// one at the appraised value — 0.00% progress beside a multi-million-baht increase.
    ///
    /// KNOWN CASE, not hidden: when land plus finished buildings alone already exceed the appraised
    /// value, the cap collapses all three milestones onto it and the card shows 0 baht of movement
    /// beside a percentage that did move. That is what the stored data says — the price of record is
    /// below what the components add up to — and the likeliest cause is a PricingFinalValues.LandValue
    /// row saved before the 2026-09-24 ApplyLandAreaValue change, which still holds a market approach's
    /// whole-property lump under a column that means land. Those rows correct themselves when the
    /// group is saved again; no backfill shipped, by decision. Smoothing the delta rows here would
    /// only make a contradiction in the data look like a clean report.
    /// </summary>
    public decimal PreviousValue => RawPreviousPercent >= 100m
        ? CompleteValue
        : Math.Min(LandValue + CompletedBuildingValue + InspectedPreviousValue, CompleteValue);

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

        // The price of record for the inspected groups. Read on both paths: it is the 100% base the
        // card and the summary book both print, and below it stands in entirely for an inspection
        // that has no value of its own.
        var appraisedValue = await connection.QueryFirstOrDefaultAsync<decimal>(
            new CommandDefinition(AppraisedValueSql, p, cancellationToken: cancellationToken));

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
                HasOwnValueBase: true,
                AppraisedValue: appraisedValue);
        }

        // No money to report — the inspected groups carry no price and this inspection has no value
        // base of its own — but the progress IS recorded, and it is not money. Returning null here
        // would say "nothing is under construction": GetAppraisalForCollateralQueryHandler passes
        // IsUnderConstruction and ConstructionProgressPercent straight from this breakdown onto the
        // frozen engagement, so a building genuinely half-finished would be recorded as not being
        // built at all, and the MIS report's progress columns would go blank with it. Report the
        // percentages with zero money instead: 0 here means "not priced yet", which is the truth,
        // and it cannot be confused with a finished building because the percentage says otherwise.
        //
        // Unscaled on purpose. A house is financed against how much of it is built, so its value
        // steps up with the percentage; a condo unit is not — the buyer is buying the finished unit
        // and nothing is drawn down per milestone. The percentage is still reported, it just does
        // not move the money.
        //
        // Land and completed buildings are dropped rather than added: AppraisedValue already covers
        // the whole of the inspected groups, so leaving them in would count them twice in
        // CurrentValue / CompleteValue / PreviousValue.
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
            HasOwnValueBase: false,
            AppraisedValue: appraisedValue);
    }

    /// <summary>
    /// The groups this breakdown is about: those holding a property under construction inspection.
    /// An appraisal that also carries machinery, a bare plot or another condo in their own groups
    /// was dragging all of it into the 100% base while the progress percentage came from the
    /// inspected buildings alone — numerator and denominator measuring different collateral.
    /// Machinery needs no filter of its own: a machinery group has no inspection, so it never
    /// enters the set. Every query below falls back to the whole appraisal when the set is empty,
    /// which happens when an inspected property belongs to no group at all.
    /// KEEP IN SYNC with AppraisalSummaryConstructionDataProvider — the report and this card print
    /// the same money, and they are only equal while they cover the same groups.
    /// </summary>
    internal const string CiGroupsSql = """
        SELECT DISTINCT gi.PropertyGroupId
                FROM appraisal.ConstructionInspections ci2
                JOIN appraisal.AppraisalProperties ap2 ON ap2.Id = ci2.AppraisalPropertyId
                JOIN appraisal.PropertyGroupItems gi ON gi.AppraisalPropertyId = ap2.Id
                WHERE ap2.AppraisalId = @AppraisalId
        """;

    /// <summary>
    /// The price of record: the 100% base every milestone is reported against, and the whole of the
    /// figure for an inspection that has no value of its own (a condo unit has no depreciation table
    /// to total). Read per group so it covers the same collateral as everything else here.
    ///
    /// The whole-appraisal valuation is consulted ONLY when no inspected property sits in a group,
    /// which is the one case where "the inspected groups" names nothing. When such a group exists but
    /// carries no price, the answer is 0: falling through to an appraisal-level figure would hand
    /// this card the machinery group's money and call it the building's, which is the very mixing the
    /// scoping exists to stop. 0 then lets CompleteValue fall back to the components, and only a
    /// condo — which has no components either — ends with no card at all.
    /// KEEP IN SYNC with AppraisalSummaryConstructionDataProvider's appraisedValue.
    /// </summary>
    private const string AppraisedValueSql = $"""
        SELECT ISNULL(
                   CASE WHEN EXISTS (SELECT 1 FROM (SELECT 1 AS One) seed
                                     WHERE EXISTS ({CiGroupsSql}))
                        THEN SUM(CASE WHEN g.InCiGroup = 1 THEN g.GroupValue END)
                        -- valuation ?? rollup, matching TotalAppraisalValue on the report side: an
                        -- appraisal priced group by group need not have a committed valuation row.
                        ELSE COALESCE(
                                 (SELECT MAX(va.AppraisedValue)
                                  FROM appraisal.ValuationAnalyses va
                                  WHERE va.AppraisalId = @AppraisalId),
                                 SUM(g.GroupValue))
                   END, 0)
        FROM (
            -- One row per group; the per-group value has to be projected here before it can be
            -- summed, because SQL Server will not aggregate over an APPLY that aggregates.
            SELECT COALESCE(pa.FinalAppraisedValue, grp.EffectiveValue) AS GroupValue,
                   CASE WHEN pg.Id IN ({CiGroupsSql}) THEN 1 ELSE 0 END AS InCiGroup
            FROM appraisal.PropertyGroups pg
            LEFT JOIN appraisal.PricingAnalysis pa
                ON pa.AnchorId = pg.Id AND pa.SubjectType = 0
            OUTER APPLY (
                SELECT SUM(COALESCE(pm.MethodValue, fv.IndicatedValue, fv.FinalValue)) AS EffectiveValue
                FROM appraisal.PricingAnalysisApproaches pap
                JOIN appraisal.PricingAnalysisMethods pm
                    ON pm.ApproachId = pap.Id AND pm.IsSelected = 1
                LEFT JOIN appraisal.PricingFinalValues fv ON fv.PricingMethodId = pm.Id
                WHERE pap.PricingAnalysisId = pa.Id AND pap.IsSelected = 1
            ) grp
            WHERE pg.AppraisalId = @AppraisalId
        ) g
        """;

    /// <summary>
    /// Land component, one value per group: only the SELECTED approach and method count. An
    /// appraiser who priced a group two ways leaves a PricingFinalValue row behind on every method
    /// tried, and summing them all counted the same land two or three times over — the summary
    /// report's RS02 filters identically, and AppraisalSummaryLandBuildingDataProvider's RS07
    /// always has. NOTE: PricingFinalValues.LandValue is only written for per-unit-rate methods
    /// (PerSqWa / PerSqm); a whole-unit lumpsum method carries no land rate and leaves it NULL by
    /// design, so this can legitimately be 0. Historical rows saved before the server-side derivation
    /// shipped also need Database/Scripts/Maintenance/BackfillPricingFinalValueLandArea.sql — which
    /// writes without an IsSelected guard, so older data is the likeliest source of duplicate rows.
    /// <para>
    /// Role filter: a Cost approach can now have up to one selected method per role (Land,
    /// LandAndBuilding, Building, Machinery — PricingAnalysisApproach.EnsureNoComponentCountedTwice
    /// enforces it), so a Building- or Machinery-role method could in principle be selected alongside
    /// the land one. Their LandValue is always NULL by construction today (only Land/LandAndBuilding
    /// writers ever populate it), so excluding them changes nothing for existing data — it just stops
    /// the SUM from silently picking up a second row if that construction-time guarantee ever drifts.
    /// Role IS NULL is still let through. As of 2026-09-23 such a row contributes nothing ONCE IT HAS
    /// BEEN SAVED AGAIN — rows untouched since then still carry their market lump, so this SUM is a
    /// mix until the data catches up (no backfill shipped, by decision). The mechanism:
    /// PricingAnalysisMethod.ApplyLandAreaValue now CLEARS the land figures for a method with no Role.
    /// Market, income and residual price the collateral as one lump, so a per-square-wa comparable
    /// rate says how the market quotes a parcel, not that the resulting figure excludes the buildings
    /// standing on it — multiplying it out wrote the whole property's value into LandValue, and this
    /// SUM then added the buildings again from BuildingDepreciationDetails. The role clause narrows to
    /// Land / LandAndBuilding (plus NULL) so a second selected method of another role cannot add a
    /// LandValue of its own. The gate that matters is still the domain one in ApplyLandAreaValue —
    /// this is a second line of defence, not a substitute, and it is inert on current data because
    /// only the land-bearing roles ever write the column. The same clause is now in RS02 of the
    /// construction book and in vw_MisCasReport's Land CTE; change one and change all three.
    /// <para>
    /// A market-priced appraisal that also has a construction inspection therefore now contributes
    /// 0 land instead of a double-counted total. Neither figure is right — this formula needs a
    /// separable land value and the market approach cannot produce one — but construction work is
    /// priced with the cost approach in practice, so the combination is not expected to arise.
    /// </para>
    /// </para>
    /// </summary>
    private const string LandValueSql = $"""
        SELECT ISNULL(SUM(pfv.LandValue), 0)
        FROM appraisal.PricingFinalValues pfv
        JOIN appraisal.PricingAnalysisMethods pam ON pam.Id = pfv.PricingMethodId
            AND pam.IsSelected = 1
            AND (pam.Role IS NULL OR pam.Role IN ('Land', 'LandAndBuilding'))
        JOIN appraisal.PricingAnalysisApproaches paa ON paa.Id = pam.ApproachId
            AND paa.IsSelected = 1
        JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId AND pa.SubjectType = 0
        JOIN appraisal.PropertyGroups pg ON pg.Id = pa.AnchorId
        WHERE pg.AppraisalId = @AppraisalId
          AND (pg.Id IN ({CiGroupsSql}) OR NOT EXISTS ({CiGroupsSql}))
        """;

    /// <summary>
    /// Buildings with no inspection — finished, so they count at full value. Per building that is
    /// the appraiser's own Building Cost Value where they keyed one, else the depreciated sum
    /// rounded to the nearest 1,000: the rule the property form, BuildingInsuranceCalculator and
    /// PricingPropertyDataService all apply. The raw sum this used to take ignored an override
    /// outright, so a building the appraiser had priced himself entered every milestone of the
    /// breakdown at the system's figure instead of his.
    /// KEEP IN SYNC with GetDecisionSummaryQueryHandler.completedBuildingSql and the construction
    /// summary book's RS03 — the three print the same money on three screens.
    /// </summary>
    private const string CompletedBuildingValueSql = $"""
        SELECT ISNULL(SUM(b.BuildingValue), 0)
        FROM (
            -- Driven from the building, LEFT JOINed to its schedule: an appraiser who keys a Building
            -- Cost Value instead of filling in a depreciation table leaves no BuildingDepreciationDetails
            -- rows at all, and driving from that table dropped exactly the building this COALESCE
            -- exists to honour. Same join direction as PricingPropertyDataService.BuildingFinalCostValuesSql.
            -- ISNULL: no schedule and no override is worth 0, not NULL.
            SELECT ISNULL(COALESCE(bad.FinalCostValueOverride,
                                   ROUND(SUM(bdd.PriceAfterDepreciation), -3)), 0) AS BuildingValue
            FROM appraisal.BuildingAppraisalDetails bad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            LEFT JOIN appraisal.BuildingDepreciationDetails bdd ON bdd.BuildingAppraisalDetailId = bad.Id
            WHERE ap.AppraisalId = @AppraisalId
              AND NOT EXISTS (
                  SELECT 1 FROM appraisal.ConstructionInspections ci
                  WHERE ci.AppraisalPropertyId = ap.Id
              )
              AND (EXISTS (
                      SELECT 1 FROM appraisal.PropertyGroupItems gi
                      WHERE gi.AppraisalPropertyId = ap.Id
                        AND gi.PropertyGroupId IN ({CiGroupsSql}))
                   OR NOT EXISTS ({CiGroupsSql}))
            GROUP BY bad.Id, bad.FinalCostValueOverride
        ) b
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
            ISNULL(SUM(ROUND(v.CurrentValue, 0)), 0)     AS CurrentValue,
            COUNT(*)                                     AS InspectionCount,
            -- Plain averages, consulted only when there is no value to weight by.
            ISNULL(AVG(v.PreviousPct), 0)                AS UnweightedPreviousPercent,
            ISNULL(AVG(v.CurrentPct), 0)                 AS UnweightedCurrentPercent,
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
                 THEN SUM(v.TotalValue * v.CurrentPct) / SUM(v.TotalValue)
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
                     ELSE ISNULL(wd.CurrentProportionPctSum, 0) END  AS CurrentPct,
                CASE WHEN ci.IsFullDetail = 0
                     THEN ci.TotalValue * ISNULL(ci.SummaryPreviousProgressPct, 0) / 100.0
                     ELSE ISNULL(wd.PreviousPropertyValueSum, 0) END AS PreviousValue,
                CASE WHEN ci.IsFullDetail = 0
                     THEN ci.TotalValue * ISNULL(ci.SummaryCurrentProgressPct, 0) / 100.0
                     ELSE ISNULL(wd.CurrentPropertyValueSum, 0) END  AS CurrentValue
        ) v
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
