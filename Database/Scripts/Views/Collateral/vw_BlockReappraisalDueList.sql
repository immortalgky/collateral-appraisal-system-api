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
    DATEDIFF(DAY, CAST(GETUTCDATE() AS date), brd.DueDate) AS RemainingDay
FROM collateral.BlockReappraisalDue brd
WHERE brd.Status = 'Pending';
