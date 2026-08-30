namespace Appraisal.Domain.Appraisals;

/// <summary>
/// The rounding rule for every baht figure a construction inspection produces: whole baht,
/// with 0.50 rounding up (CA-614 — the inspection reports must not print satang).
///
/// Rounding happens where the values are computed and persisted rather than at each place that
/// displays them, so a row-by-row printout and the total beside it are rounded once, consistently.
/// Rounding per display site instead lets the two disagree — two work items ending in .50 round up
/// individually (+1 each) but cancel exactly when summed first.
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
/// decimal places, which is what the business asked for. That separation is what makes the rounding
/// safe. Construction progress, and with it whether a building counts as finished, is read from the
/// entered percentages (ConstructionValueBreakdown.ConstructionProgressPercent), never from a ratio
/// of rounded money — the six places that report progress all take that route, in the Appraisal,
/// Reporting, Collateral and Integration modules. Adding a seventh means reading percentages there
/// too: dividing these amounts back out is what CA-614's rounding made inexact.
///
/// Inspections already in the database are deliberately left as they were, with no migration. They
/// are the record of a round that was already inspected, some of it already exported to the
/// regulator and frozen into CollateralEngagement, and the difference is under a baht. So a
/// full-detail inspection saved before this rule keeps its satang on the report until someone edits
/// and saves it again; summary mode has no such lag, since its figure is derived at read time.
/// Correcting a named appraisal is a data-correction exercise on that appraisal.
/// </summary>
internal static class ConstructionMoney
{
    public static decimal ToBaht(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);

    public static decimal? ToBaht(decimal? value) =>
        value is null ? null : ToBaht(value.Value);
}
