namespace Appraisal.Domain.Appraisals;

/// <summary>
/// The rounding rule for every baht figure a construction inspection produces: whole baht,
/// with 0.50 rounding up (CA-614 — the inspection reports must not print satang).
///
/// Rounding happens where the values are computed and persisted rather than at each place that
/// displays them, because every consumer downstream only SUMs the stored columns: a sum of whole
/// baht is whole baht, so the detail rows still add up to the total printed beside them. Rounding
/// per display site instead lets a row-by-row printout disagree with its own total — two work
/// items ending in .50 round up individually (+1 each) but cancel exactly when summed first.
///
/// A summary-mode inspection stores no computed value — its figure is derived at read time as
/// TotalValue * progressPct / 100 — so the same rule is mirrored as ROUND(..., 0) in the three
/// places that repeat that formula:
///   Appraisal  — ConstructionCurrentValueService.CiAggregateSql
///   Reporting  — AppraisalSummaryConstructionDataProvider, which keeps its own copy of the
///                aggregate so it can batch the whole report into one round-trip
///   Collateral — Database/Scripts/Views/Collateral/vw_RegulatoryExport.sql
/// SQL Server's ROUND rounds halves away from zero, matching MidpointRounding.AwayFromZero here.
/// Change one and the other three have to follow, or the same appraisal prints different totals
/// on the summary report, the appraisal book and the regulatory export.
///
/// Percentages are NOT rounded by this rule — they are stored as decimal(7,4) and reported to two
/// decimal places, which is what the business asked for.
/// </summary>
internal static class ConstructionMoney
{
    public static decimal ToBaht(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);

    public static decimal? ToBaht(decimal? value) =>
        value is null ? null : ToBaht(value.Value);
}
