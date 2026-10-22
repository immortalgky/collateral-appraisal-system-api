CREATE OR ALTER VIEW collateral.vw_BlockReappraisalDueList AS
-- The list's two tabs: Pending (due) and Deleted (marked "reappraisal not required").
-- Consumed rows (a request was raised) are left out.
SELECT
    brd.CollateralMasterId,
    brd.OldAppraisalNumber,
    brd.ProjectName,
    brd.ProjectType,
    brd.ProjectSellingPrice,
    brd.TotalUnits,
    brd.RemainingUnits,
    brd.LastAppraisedDate,
    brd.DueDate,
    brd.Status,
    -- Server-local (Bangkok) today via GETDATE(), never UTC — matches the bank calendar.
    -- Negative when the due date has already passed (overdue).
    DATEDIFF(DAY, CAST(GETDATE() AS date), brd.DueDate) AS RemainingDay,
    brd.NotRequiredRemark
FROM collateral.BlockReappraisalDue brd
WHERE brd.Status IN ('Pending', 'Deleted');
