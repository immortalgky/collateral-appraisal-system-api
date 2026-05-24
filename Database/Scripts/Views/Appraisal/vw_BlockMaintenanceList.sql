CREATE
OR ALTER
VIEW appraisal.vw_BlockMaintenanceList AS
SELECT p.Id                                                                     AS ProjectId,
       p.AppraisalId,
       a.AppraisalNumber                                                        AS AppraisalReportNo,
       c.Name                                                                   AS CustomerName,
       p.ProjectName,
       p.ProjectType,
       p.Developer,
       COUNT(pu.Id)                                                             AS TotalUnits,
       SUM(CAST(pu.IsSold AS INT))                                             AS SoldUnits,
       COUNT(pu.Id) - SUM(CAST(pu.IsSold AS INT))                             AS UnsoldUnits,
       p.UpdatedAt                                                              AS UpdatedOn,
       p.UpdatedBy                                                              AS UpdatedBy
FROM appraisal.Projects p
         LEFT JOIN appraisal.Appraisals a ON a.Id = p.AppraisalId
         OUTER APPLY (SELECT TOP 1 rc.Name
                      FROM request.RequestCustomers rc
                      WHERE rc.RequestId = a.RequestId) c
         LEFT JOIN appraisal.ProjectUnits pu ON pu.ProjectId = p.Id
WHERE a.IsDeleted = 0
GROUP BY p.Id,
         p.AppraisalId,
         a.AppraisalNumber,
         c.Name,
         p.ProjectName,
         p.ProjectType,
         p.Developer,
         p.UpdatedAt,
         p.UpdatedBy
