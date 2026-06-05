CREATE OR ALTER VIEW collateral.vw_BlockReappraisalDueList AS
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
    -- Server-local (Bangkok) today via GETDATE(), never UTC — matches the bank calendar.
    -- Negative when the due date has already passed (overdue).
    DATEDIFF(DAY, CAST(GETDATE() AS date), brd.DueDate) AS RemainingDay
FROM collateral.BlockReappraisalDue brd
WHERE brd.Status = 'Pending';
