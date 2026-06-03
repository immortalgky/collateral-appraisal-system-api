CREATE
OR ALTER
VIEW appraisal.vw_AppointmentList AS
SELECT ap.Id,
       ap.AssignmentId,
       aa.AppraisalId,
       ap.AppointmentDateTime,
       ap.ProposedDate,
       ap.LocationDetail,
       ap.Latitude,
       ap.Longitude,
       ap.Status,
       ap.Reason,
       ap.ApprovedBy,
       ap.ApprovedAt,
       ap.RescheduleCount,
       ap.AppointedBy,
       ap.ContactPerson,
       ap.ContactPhone,
       ap.CreatedAt,
       ap.RequiresApproval,
       ap.ApprovalSubmittedAt,
       -- The date the appointment held before the current pending reschedule (for the
       -- "old date struck-through vs new date" display). Only populated while approval is
       -- required; sourced from the most recent 'Rescheduled' history entry.
       CASE
           WHEN ap.RequiresApproval = 1 THEN (
               SELECT TOP 1 h.PreviousAppointmentDateTime
               FROM appraisal.AppointmentHistory h
               WHERE h.AppointmentId = ap.Id
                 AND h.ChangeType = 'Rescheduled'
               ORDER BY h.ChangedAt DESC
           )
       END AS PreviousDate
FROM appraisal.Appointments ap
         INNER JOIN appraisal.AppraisalAssignments aa ON aa.Id = ap.AssignmentId
