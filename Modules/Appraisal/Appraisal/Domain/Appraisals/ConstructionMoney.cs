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
/// TotalValue * progressPct / 100 — so the same rule is mirrored as ROUND(..., 0) in the four
/// places that repeat that formula:
///   Appraisal  — ConstructionCurrentValueService.CiAggregateSql
///   Appraisal  — GetDecisionSummaryQueryHandler.ciDetailSql, the card's per-building rows
///   Reporting  — AppraisalSummaryConstructionDataProvider, which keeps its own copy of the
///                aggregate so it can batch the whole report into one round-trip
///   Collateral — Database/Scripts/Views/Collateral/vw_RegulatoryExport.sql
/// SQL Server's ROUND rounds halves away from zero, matching MidpointRounding.AwayFromZero here.
/// Change one and the other three have to follow, or the same appraisal prints different totals
/// on the summary report, the appraisal book, the Decision Summary card and the regulatory export.
///
/// Percentages are NOT rounded to baht by this rule — they are stored as decimal(7,4) and reported to two
/// decimal places, which is what the business asked for. That separation is what makes the rounding
/// safe. Construction progress, and with it whether a building counts as finished, is read from the
/// entered percentages (ConstructionValueBreakdown.ConstructionProgressPercent), never from a ratio
/// of rounded money. Nine places report progress, across the Appraisal, Reporting, Collateral and
/// Integration modules, and every one of them reads the percentages: IConstructionCurrentValueService
/// (the reference), the construction summary report, the land-and-building summary report, the
/// appraisal book's progress table and its rollups, the Decision Summary card and its per-building
/// rows, collateral.vw_RegulatoryExport, the AS400 appraisal-result file and
/// reporting.vw_RCAS004_ConstructionInspection, the committee agenda and minute
/// (MeetingMinuteDataProvider), plus the inspection screen and the Decision Summary tables on the
/// front end. Adding another means reading the percentages there too: dividing these
/// amounts back out is exactly what CA-614's rounding made inexact.
///
/// The one-off scripts under Database/Scripts/Maintenance still divide the money. They are left as
/// they were on purpose — they are not part of the running system.
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
