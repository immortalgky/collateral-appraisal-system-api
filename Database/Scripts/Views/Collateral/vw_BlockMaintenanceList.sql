CREATE
OR ALTER
VIEW collateral.vw_BlockMaintenanceList AS
SELECT cm.Id                        AS CollateralMasterId,
       pd.LastAppraisalNumber       AS AppraisalReportNo,
       cm.CustomerName,
       pd.ProjectName,
       pd.ProjectType,
       pd.Developer,
       COUNT(pu.Id)                                          AS TotalUnits,
       ISNULL(SUM(CAST(pu.IsSold AS INT)), 0)                AS SoldUnits,
       COUNT(pu.Id) - ISNULL(SUM(CAST(pu.IsSold AS INT)), 0) AS UnsoldUnits,
       cm.UpdatedAt                 AS UpdatedOn,
       cm.UpdatedBy                 AS UpdatedBy
FROM collateral.CollateralMasters cm
         INNER JOIN collateral.ProjectDetails pd ON pd.CollateralMasterId = cm.Id
         LEFT JOIN collateral.ProjectUnits pu ON pu.CollateralMasterId = cm.Id
WHERE cm.CollateralType = 'PRJ'
  AND cm.IsMaster = 1
  AND cm.IsDeleted = 0
  AND pd.IsDeleted = 0
GROUP BY cm.Id,
         pd.LastAppraisalNumber,
         cm.CustomerName,
         pd.ProjectName,
         pd.ProjectType,
         pd.Developer,
         cm.UpdatedAt,
         cm.UpdatedBy
