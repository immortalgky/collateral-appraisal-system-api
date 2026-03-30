CREATE
OR ALTER
VIEW workflow.vw_TaskList
AS
SELECT a.Id,
       pt.Id                               AS TaskId,
       pt.WorkflowInstanceId,
       pt.ActivityId,
       a.AppraisalNumber,
       c.Name                             AS CustomerName,
       CAST(pt.TaskName AS nvarchar(100)) AS TaskType,
       pt.TaskDescription,
       r.Purpose,
       p.PropertyType                     AS PropertyType,
       a.Status,
       ap.AppointmentDateTime,
       pt.AssignedTo                      AS AssigneeUserId,
       r.RequestedAt,
       pt.AssignedAt                      AS ReceivedDate,
       CAST(pt.TaskName AS nvarchar(255)) AS Movement,
       a.SLADays,
       CAST(null AS int)                  AS OLAActual,
       CAST(null AS int)                  AS OLADiff,
       a.Priority,
       pt.DueAt,
       pt.SlaStatus,
       DATEDIFF(HOUR, pt.AssignedAt, GETUTCDATE())                                        AS ElapsedHours,
       CASE WHEN pt.DueAt IS NOT NULL THEN DATEDIFF(HOUR, GETUTCDATE(), pt.DueAt) END     AS RemainingHours
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
         LEFT JOIN appraisal.AppraisalAssignments AA
on AA.AppraisalId = a.Id AND AA.AssignmentStatus != 'Rejected' AND AA.AssignmentStatus != 'Cancelled'
    LEFT JOIN appraisal.Appointments ap ON ap.AssignmentId = aa.Id AND ap.Status != 'Cancelled'
