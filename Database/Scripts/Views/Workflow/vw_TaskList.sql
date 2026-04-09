CREATE
OR ALTER
VIEW workflow.vw_TaskList
AS
SELECT a.Id,
       pt.Id                                                                           AS TaskId,
       pt.WorkflowInstanceId,
       pt.ActivityId,
       a.AppraisalNumber,
       c.Name                                                                          AS CustomerName,
       CAST(pt.TaskName AS nvarchar(100))                                              AS TaskType,
       pt.TaskDescription,
       r.Purpose,
       p.PropertyType                                                                  AS PropertyType,
       a.Status,
       pt.TaskStatus                                                                   AS PendingTaskStatus,
       ap.AppointmentDateTime,
       pt.AssignedTo                                                                   AS AssigneeUserId,
       a.RequestedBy,
       COALESCE(a.RequestedAt, r.RequestedAt)                                          AS RequestReceivedDate,
       pt.AssignedAt                                                                   AS AssignedDate,
       pt.Movement,
       AA.InternalAppraiserName                                                        AS InternalFollowupStaff,
       CASE
           WHEN AA.AssignmentType = 'Internal' THEN AA.InternalAppraiserName
           WHEN AA.AssignmentType = 'External' THEN comp.Name
       END                                                                             AS Appraiser,
       a.Priority,
       pt.DueAt,
       pt.SlaStatus,
       DATEDIFF(HOUR, pt.AssignedAt, GETUTCDATE())                                     AS ElapsedHours,
       CASE WHEN pt.DueAt IS NOT NULL THEN DATEDIFF(HOUR, GETUTCDATE(), pt.DueAt) END  AS RemainingHours
FROM workflow.PendingTasks pt
         JOIN appraisal.Appraisals a ON a.Id = pt.CorrelationId
         JOIN request.Requests r ON a.RequestId = r.Id
    CROSS APPLY (SELECT TOP 1 Name
                      FROM request.RequestCustomers
                      WHERE RequestId = r.Id) C
         CROSS APPLY (SELECT STRING_AGG(PropertyType, ',') AS PropertyType
                      FROM request.RequestProperties
                      WHERE RequestId = r.Id
                      GROUP BY RequestId) P
         OUTER APPLY (SELECT TOP 1 Id, AssignmentType, InternalAppraiserName, AssigneeCompanyId
                      FROM appraisal.AppraisalAssignments
                      WHERE AppraisalId = a.Id
                        AND AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                      ORDER BY AssignedAt DESC) AA
         OUTER APPLY (SELECT TOP 1 AppointmentDateTime
                      FROM appraisal.Appointments
                      WHERE AssignmentId = AA.Id
                        AND Status != 'Cancelled') ap
         LEFT JOIN auth.Companies comp ON comp.Id = TRY_CAST(AA.AssigneeCompanyId AS uniqueidentifier)
