CREATE
OR ALTER
VIEW appraisal.vw_AppraisalDetail AS
SELECT a.Id,
       a.AppraisalNumber,
       a.RequestId,
       a.RequestedAt,
       -- Derive status: CompletedAt trumps all; then workflow activity; fallback to stored status
       CASE
           WHEN a.CompletedAt IS NOT NULL THEN 'Completed'
           WHEN wpt.ActivityId IN ('appraisal-initiation-check', 'appraisal-assignment') THEN 'Pending'
           WHEN wpt.ActivityId IN ('appraisal-book-verification', 'int-appraisal-check', 'int-appraisal-verification', 'pending-approval') THEN 'UnderReview'
           WHEN wpt.ActivityId IS NOT NULL THEN 'InProgress'
           ELSE a.Status
       END AS Status,
       a.AppraisalType,
       a.Priority,
       a.SLAHours,
       a.SLADueDate,
       a.SLAStatus,
       CAST(a.SLAHours AS DECIMAL(9,2)) / 8.0                                               AS SLABusinessDays,
       a.ActualHoursToComplete,
       a.IsWithinSLA,
       (SELECT COUNT(*) FROM appraisal.AppraisalProperties ap WHERE ap.AppraisalId = a.Id)  AS PropertyCount,
       (SELECT COUNT(*) FROM appraisal.PropertyGroups pg WHERE pg.AppraisalId = a.Id)       AS GroupCount,
       (SELECT COUNT(*) FROM appraisal.AppraisalAssignments aa WHERE aa.AppraisalId = a.Id) AS AssignmentCount,
       a.CreatedAt,
       a.CreatedBy,
       a.UpdatedAt,
       a.UpdatedBy,
       -- Block appraisal detection: presence of a Projects row is authoritative
       CAST(CASE WHEN p.Id IS NOT NULL THEN 1 ELSE 0 END AS BIT)                           AS IsBlock,
       CASE p.ProjectType WHEN 1 THEN 'Condo' WHEN 2 THEN 'LandAndBuilding' ELSE NULL END  AS BlockProjectType
FROM appraisal.Appraisals a
         OUTER APPLY (SELECT TOP 1 pt.ActivityId
                      FROM workflow.PendingTasks pt
                      WHERE pt.CorrelationId = a.RequestId
                      ORDER BY pt.AssignedAt DESC) wpt
         OUTER APPLY (SELECT TOP 1 ip.Id, ip.ProjectType
                      FROM appraisal.Projects ip
                      WHERE ip.AppraisalId = a.Id) p
