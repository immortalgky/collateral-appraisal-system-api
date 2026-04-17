CREATE
OR ALTER
VIEW appraisal.vw_AppraisalList AS
SELECT a.Id,
       a.AppraisalNumber,
       a.RequestId,
       r.RequestNumber,
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
       a.IsPma,
       a.Purpose,
       a.Channel,
       a.BankingSegment,
       a.FacilityLimit,
       a.RequestedBy,
       a.RequestedAt,
       a.SLADays,
       a.SLADueDate,
       a.SLAStatus,
       a.CreatedAt,
       (SELECT COUNT(*) FROM appraisal.AppraisalProperties ap WHERE ap.AppraisalId = a.Id) AS PropertyCount,
       -- Latest active assignment info
       la.AssigneeUserId,
       la.AssigneeCompanyId,
       la.AssignmentType,
       la.AssignmentStatus,
       la.AssignedAt                                                                       AS AssignedDate,
       -- Company name for external assignments
       comp.Name                                                                           AS CompanyName,
       -- Customer name from request
       c.Name                                                                              AS CustomerName,
       -- First property location
       ld.Province,
       ld.District,
       -- Latest appointment
       apt.AppointmentDateTime,
       -- SLA computed fields
       DATEDIFF(HOUR, a.CreatedAt, GETUTCDATE())                                           AS ElapsedHours,
       CASE
           WHEN a.SLADueDate IS NOT NULL
               THEN DATEDIFF(HOUR, GETUTCDATE(), a.SLADueDate)
           END                                                                             AS RemainingHours
FROM appraisal.Appraisals a
         LEFT JOIN request.Requests r ON r.Id = a.RequestId
         OUTER APPLY (SELECT TOP 1 Name
                      FROM request.RequestCustomers
                      WHERE RequestId = a.RequestId) c
         LEFT JOIN (SELECT aa.Id,
                           aa.AppraisalId,
                           aa.AssigneeUserId,
                           aa.AssigneeCompanyId,
                           aa.AssignmentType,
                           aa.AssignmentStatus,
                           aa.AssignedAt,
                           ROW_NUMBER() OVER (PARTITION BY aa.AppraisalId ORDER BY aa.AssignedAt DESC) AS rn
                    FROM appraisal.AppraisalAssignments aa
                    WHERE aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')) la
                   ON la.AppraisalId = a.Id AND la.rn = 1
         LEFT JOIN auth.Companies comp
                   ON comp.Id = TRY_CAST(la.AssigneeCompanyId AS uniqueidentifier)
         LEFT JOIN (SELECT ap2.AppraisalId,
                           lad.Province,
                           lad.District,
                           ROW_NUMBER() OVER (PARTITION BY ap2.AppraisalId ORDER BY ap2.SequenceNumber) AS rn
                    FROM appraisal.AppraisalProperties ap2
                             JOIN appraisal.LandAppraisalDetails lad ON lad.AppraisalPropertyId = ap2.Id
                    WHERE lad.Province IS NOT NULL) ld ON ld.AppraisalId = a.Id AND ld.rn = 1
         OUTER APPLY (SELECT TOP 1 AppointmentDateTime
                      FROM appraisal.Appointments
                      WHERE AssignmentId = la.Id
                        AND Status != 'Cancelled'
                      ORDER BY AppointmentDateTime DESC) apt
         -- Current workflow activity for status derivation
         OUTER APPLY (SELECT TOP 1 pt.ActivityId
                      FROM workflow.PendingTasks pt
                      WHERE pt.CorrelationId = a.RequestId
                      ORDER BY pt.AssignedAt DESC) wpt
WHERE a.IsDeleted = 0
