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
       ap.CreatedAt
FROM appraisal.Appointments ap
         INNER JOIN appraisal.AppraisalAssignments aa ON aa.Id = ap.AssignmentId
