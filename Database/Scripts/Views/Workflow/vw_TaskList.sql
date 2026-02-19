CREATE
OR ALTER
VIEW workflow.vw_TaskList
AS
SELECT a.Id,
       a.AppraisalNumber,
       c.Name                      AS CustomerName,
       ''                          AS TaskType,
       r.Purpose,
       p.PropertyType              AS PropertyType,
       a.Status,
       ap.AppointmentDateTime,
       AA.AssigneeUserId,
       r.RequestedAt,
       CAST(null AS datetime)      AS ReceivedDate,
       CAST(null AS nvarchar(255)) AS Movement,
       a.SLADays,
       CAST(null AS int)           AS OLAActual,
       CAST(null AS int)           AS OLADiff,
       a.Priority
FROM appraisal.Appraisals a
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
    LEFT JOIN appraisal.Appointments ap ON ap.AssignmentId = aa.Id