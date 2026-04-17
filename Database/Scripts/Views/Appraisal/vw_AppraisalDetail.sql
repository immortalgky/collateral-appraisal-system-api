CREATE
OR ALTER
VIEW appraisal.vw_AppraisalDetail AS
SELECT a.Id,
       a.AppraisalNumber,
       a.RequestId,
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
       a.SLADays,
       a.SLADueDate,
       a.SLAStatus,
       a.ActualDaysToComplete,
       a.IsWithinSLA,
       (SELECT COUNT(*) FROM appraisal.AppraisalProperties ap WHERE ap.AppraisalId = a.Id)  AS PropertyCount,
       (SELECT COUNT(*) FROM appraisal.PropertyGroups pg WHERE pg.AppraisalId = a.Id)       AS GroupCount,
       (SELECT COUNT(*) FROM appraisal.AppraisalAssignments aa WHERE aa.AppraisalId = a.Id) AS AssignmentCount,
       a.CreatedAt,
       a.CreatedBy,
       a.UpdatedAt,
       a.UpdatedBy
FROM appraisal.Appraisals a
         OUTER APPLY (SELECT TOP 1 pt.ActivityId
                      FROM workflow.PendingTasks pt
                      WHERE pt.CorrelationId = a.RequestId
                      ORDER BY pt.AssignedAt DESC) wpt
