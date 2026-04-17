CREATE
OR ALTER
VIEW workflow.vw_TaskList
AS
SELECT pt.Id                                                                              AS Id,
       a.Id                                                                               AS AppraisalId,
       r.Id                                                                               AS RequestId,
       pt.WorkflowInstanceId,
       pt.ActivityId,
       a.AppraisalNumber,
       r.RequestNumber,
       c.Name                                                                             AS CustomerName,
       pt.TaskName                                                                        AS TaskType,
       pt.TaskDescription,
       r.Purpose,
       p.PropertyType                                                                     AS PropertyType,
       -- Derive appraisal status: CompletedAt trumps all; then workflow activity; fallback to request status
       CASE
           WHEN a.CompletedAt IS NOT NULL THEN 'Completed'
           WHEN pt.ActivityId IN ('appraisal-initiation-check', 'appraisal-assignment') THEN 'Pending'
           WHEN pt.ActivityId IN ('appraisal-book-verification', 'int-appraisal-check', 'int-appraisal-verification', 'pending-approval') THEN 'UnderReview'
           WHEN a.Id IS NOT NULL THEN 'InProgress'
           ELSE r.Status
       END AS Status,
       pt.TaskStatus                                                                      AS PendingTaskStatus,
       ap.AppointmentDateTime,
       pt.AssignedTo                                                                      AS AssigneeUserId,
       pt.AssignedType                                                                    AS AssignedType,
       COALESCE(a.RequestedBy, r.Requestor)                                               AS RequestedBy,
       COALESCE(CONCAT(u.FirstName, ' ', u.LastName), ISNULL(a.RequestedBy, r.Requestor)) AS RequestedByName,
       COALESCE(a.RequestedAt, r.RequestedAt)                                             AS RequestReceivedDate,
       pt.AssignedAt                                                                      AS AssignedDate,
       pt.Movement,
       AA.InternalAppraiserId                                                             AS InternalFollowupStaff,
       CASE
           WHEN AA.AssignmentType = 'Internal' THEN AA.InternalAppraiserId
           WHEN AA.AssignmentType = 'External' THEN comp.Name
           END                                                                            AS Appraiser,
       COALESCE(a.Priority, r.Priority)                                                   AS Priority,
       pt.DueAt,
       pt.SlaStatus,
       DATEDIFF(HOUR, pt.AssignedAt, GETUTCDATE())                                        AS ElapsedHours,
       CASE WHEN pt.DueAt IS NOT NULL THEN DATEDIFF(HOUR, GETUTCDATE(), pt.DueAt) END     AS RemainingHours,
       pt.WorkingBy,
       pt.LockedAt
FROM workflow.PendingTasks pt
         LEFT JOIN appraisal.Appraisals a ON a.RequestId = pt.CorrelationId
         LEFT JOIN request.Requests r ON r.Id = pt.CorrelationId OUTER APPLY (SELECT TOP 1 Name
                          FROM request.RequestCustomers
                          WHERE RequestId = r.Id) C
        OUTER APPLY (SELECT STRING_AGG(PropertyType, ',') AS PropertyType
                      FROM request.RequestProperties
                      WHERE RequestId = r.Id
                      GROUP BY RequestId) P
        OUTER APPLY (SELECT TOP 1 Id, AssignmentType, InternalAppraiserId, AssigneeCompanyId
                      FROM appraisal.AppraisalAssignments
                      WHERE AppraisalId = a.Id
                        AND AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                      ORDER BY AssignedAt DESC) AA
        OUTER APPLY (SELECT TOP 1 AppointmentDateTime
                      FROM appraisal.Appointments
                      WHERE AssignmentId = AA.Id
                        AND Status != 'Cancelled') ap
        LEFT JOIN auth.Companies comp
ON comp.Id = TRY_CAST(AA.AssigneeCompanyId AS uniqueidentifier)
    LEFT JOIN auth.AspNetUsers u ON u.UserName = ISNULL(a.RequestedBy, r.Requestor)

