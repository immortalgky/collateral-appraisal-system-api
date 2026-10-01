namespace Collateral.Application.Features.Reappraisal.GetCandidates;

/// <summary>
/// The "processed" tab: books whose reappraisal request has been submitted (status Consumed) — the
/// whole history, on AS400's latest file or not.
/// </summary>
internal static class ProcessedBooksSql
{
    /// <summary>
    /// One row per book under a collateral (`c`, the most recently listed copy: books were one row per
    /// file before they were deduplicated) and the reappraisal it produced (`na`), by the same rule as
    /// reporting.vw_RCAS002_ReappraisalDue: newest non-cancelled first. No `na` row = none found.
    /// </summary>
    public const string From = """
        FROM (
            SELECT v.Id, v.Status, v.ReviewType, v.ReviewDate, v.OldAppraisalReportNumber,
                   v.NormalizedSurveyNumber, v.CifNumber, v.CustomerName, v.CollateralId, v.CollateralName,
                   v.PriorAppraisalSource, v.FirstSeenFileDate, v.LastSeenFileDate, v.IsBlockUnit,
                   ROW_NUMBER() OVER (PARTITION BY v.CollateralId, v.NormalizedSurveyNumber
                                      ORDER BY v.LastSeenFileDate DESC, v.Id) AS Rn
            FROM collateral.vw_ReappraisalCandidates v
            WHERE v.Status = 'Consumed'
        ) c
        OUTER APPLY (
            SELECT TOP 1 rb.AppraisalId, rb.AppraisalNumber, rb.Status, rb.GroupTag, rb.CompletedAt,
                         r.RequestedAt AS SubmittedAt
            FROM appraisal.vw_ReappraisalsByBook rb
            LEFT JOIN request.Requests r ON r.Id = rb.RequestId
            WHERE rb.BookNumber = c.NormalizedSurveyNumber
              -- A block-project unit: only the reappraisal raised for this collateral.
              AND (c.IsBlockUnit = 0 OR rb.CollateralId = c.CollateralId)
            ORDER BY CASE WHEN rb.Status = 'Cancelled' THEN 1 ELSE 0 END, rb.CreatedAt DESC
        ) na
        OUTER APPLY (
            SELECT TOP 1 cu.MatchedUnits, u.TowerName, u.Floor, u.RoomNumber, u.HouseNumber, u.PlotNumber
            FROM collateral.vw_ReappraisalCandidateUnits cu
            LEFT JOIN appraisal.ProjectUnits u ON u.Id = cu.ProjectUnitId
            WHERE cu.CandidateId = c.Id
        ) un
        WHERE c.Rn = 1
        """;

    /// <summary>State of the new reappraisal, for the tab's quick filters.</summary>
    public static string? StatePredicate(string? state) => state?.Trim() switch
    {
        "Appraising" => "na.AppraisalId IS NOT NULL AND na.Status NOT IN ('Completed', 'Cancelled')",
        "Completed" => "na.Status = 'Completed'",
        "Cancelled" => "na.Status = 'Cancelled'",
        "NotFound" => "na.AppraisalId IS NULL",
        _ => null,
    };
}
