CREATE
OR ALTER
VIEW appraisal.vw_AssignmentList AS
SELECT a.Id,
       a.AppraisalId,
       a.AssignmentType,
       a.AssignmentStatus,
       a.AssigneeUserId,
       a.AssigneeCompanyId,
       a.ExternalAppraiserName,
       a.AssignmentMethod,
       a.ReassignmentNumber,
       a.ProgressPercent,
       a.AssignedAt,
       a.AssignedBy,
       a.StartedAt,
       a.SubmittedAt,
       a.CompletedAt,
       a.RejectionReason,
       a.CancellationReason,
       a.SLADueDate,
       DATEDIFF(day, a.StartedAt, a.SubmittedAt)                    AS ActualDaysToComplete,
       CASE
           WHEN a.SLADueDate IS NULL                       THEN NULL
           WHEN a.SubmittedAt IS NOT NULL
                AND a.SubmittedAt <= a.SLADueDate           THEN 'MetSLA'
           WHEN a.SubmittedAt IS NOT NULL                   THEN 'MissedSLA'
           WHEN GETDATE() > a.SLADueDate                    THEN 'Breached'
           WHEN DATEDIFF(day, GETDATE(), a.SLADueDate) < 2  THEN 'AtRisk'
           ELSE 'OnTrack'
       END                                                           AS SLAStatus,
       CASE
           WHEN a.SLADueDate IS NULL OR a.SubmittedAt IS NULL THEN NULL
           WHEN a.SubmittedAt <= a.SLADueDate                 THEN CAST(1 AS BIT)
           ELSE CAST(0 AS BIT)
       END                                                           AS IsWithinSLA,
       a.CreatedAt
FROM appraisal.AppraisalAssignments a
WHERE a.AssignmentStatus != 'Rejected' AND a.AssignmentStatus != 'Cancelled'
