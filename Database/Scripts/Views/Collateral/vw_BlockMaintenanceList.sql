CREATE
OR ALTER
VIEW collateral.vw_BlockMaintenanceList AS
SELECT cm.Id                  AS CollateralMasterId,
       pd.LastAppraisalNumber AS AppraisalReportNo,
       cm.CustomerName,
       pd.ProjectName,
       pd.ProjectType,
       pd.Developer,
       agg.TotalUnits,
       agg.SoldUnits,
       agg.UnsoldUnits,
       la.UpdatedOn,
       la.UpdatedBy
FROM collateral.CollateralMasters cm
         INNER JOIN collateral.ProjectDetails pd ON pd.CollateralMasterId = cm.Id
         OUTER APPLY (
    SELECT COUNT(pu.Id)                                          AS TotalUnits,
           ISNULL(SUM(CAST(pu.IsSold AS INT)), 0)                AS SoldUnits,
           COUNT(pu.Id) - ISNULL(SUM(CAST(pu.IsSold AS INT)), 0) AS UnsoldUnits
    FROM collateral.ProjectUnits pu
    WHERE pu.CollateralMasterId = cm.Id
    ) agg
         CROSS APPLY (
    -- "Last Updated" = the most recent change to the project: the master row itself
    -- or any of its unit rows (a unit sale-status edit stamps only the unit rows).
    -- TOP 1 by UpdatedAt keeps the matching UpdatedBy (the actor), unlike a bare MAX().
    SELECT TOP 1 x.UpdatedAt AS UpdatedOn, x.UpdatedBy AS UpdatedBy
    FROM (
             SELECT cm.UpdatedAt, cm.UpdatedBy
             UNION ALL
             SELECT pu.UpdatedAt, pu.UpdatedBy
             FROM collateral.ProjectUnits pu
             WHERE pu.CollateralMasterId = cm.Id
         ) x
    ORDER BY x.UpdatedAt DESC
    ) la
WHERE cm.CollateralType = 'PRJ'
  AND cm.IsMaster = 1
  AND cm.IsDeleted = 0
  AND pd.IsDeleted = 0
