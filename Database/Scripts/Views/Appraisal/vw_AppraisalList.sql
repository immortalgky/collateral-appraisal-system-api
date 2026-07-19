CREATE
OR ALTER
VIEW appraisal.vw_AppraisalList AS
SELECT a.Id,
       a.AppraisalNumber,
       a.RequestId,
       r.RequestNumber,
       a.Status AS Status,
       a.AppraisalType,
       a.Priority,
       a.IsPma,
       a.Purpose,
       a.Channel,
       a.BankingSegment,
       a.FacilityLimit,
       a.GroupTag,
       a.RequestedBy,
       a.RequestedAt,
       a.SLAHours,
       a.SLADueDate,
       a.SLAStatus,
       CAST(a.SLAHours AS DECIMAL(9,2)) / 8.0                                               AS SLABusinessDays,
       a.CreatedAt,
       va.AppraisedValue                                                                    AS AppraisalValue,
       (SELECT COUNT(*) FROM appraisal.AppraisalProperties ap WHERE ap.AppraisalId = a.Id) AS PropertyCount,
       -- Latest active assignment info
       la.AssigneeUserId,
       la.AssigneeCompanyId,
       -- The bank's own appraiser following up an EXTERNAL assignment. AssigneeUserId is only
       -- populated for Internal assignments, so consumers that need "the internal staff on this
       -- book" must pick by AssignmentType (see reporting.vw_RCAS_OlaBase).
       la.InternalAppraiserId,
       la.InternalAppraiserName,
       la.ExternalAppraiserId,
       la.ExternalAppraiserName,
       la.AssignmentType,
       la.AssignmentStatus,
       la.AssignedAt                                                                       AS AssignedDate,
       la.SubmittedAt,   -- first-submission timestamp (external: sent-to-bank; internal: execution→check); SLA end-point
       -- Company name for external assignments
       comp.Name                                                                           AS CompanyName,
       -- Customer name from request
       c.Name                                                                              AS CustomerName,
       -- First property location
       ld.Province,
       ld.District,
       ld.SubDistrict,
       -- Latest appointment
       apt.AppointmentDateTime
       -- ElapsedHours / RemainingHours are computed in C# (GetAppraisalsQueryHandler) using
       -- IBusinessTimeCalculator so they exclude weekends, holidays and lunch. They are NOT
       -- derived here: a SQL DATEDIFF would count calendar hours (nights/weekends included).
       -- CreatedAt (elapsed start) and SLADueDate (remaining end) are already exposed above.
FROM appraisal.Appraisals a
         LEFT JOIN request.Requests r ON r.Id = a.RequestId
         OUTER APPLY (SELECT TOP 1 Name
                      FROM request.RequestCustomers
                      WHERE RequestId = a.RequestId) c
         LEFT JOIN (SELECT aa.Id,
                           aa.AppraisalId,
                           aa.AssigneeUserId,
                           aa.AssigneeCompanyId,
                           aa.InternalAppraiserId,
                           aa.InternalAppraiserName,
                           aa.ExternalAppraiserId,
                           aa.ExternalAppraiserName,
                           aa.AssignmentType,
                           aa.AssignmentStatus,
                           aa.AssignedAt,
                           aa.SubmittedAt,
                           ROW_NUMBER() OVER (PARTITION BY aa.AppraisalId ORDER BY aa.AssignedAt DESC, aa.CreatedAt DESC, aa.Id DESC) AS rn
                    FROM appraisal.AppraisalAssignments aa
                    WHERE aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')) la
                   ON la.AppraisalId = a.Id AND la.rn = 1
         LEFT JOIN auth.Companies comp
                   ON comp.Id = TRY_CAST(la.AssigneeCompanyId AS uniqueidentifier)
         LEFT JOIN (SELECT ap2.AppraisalId,
                           lad.Province,
                           lad.District,
                           lad.SubDistrict,
                           ROW_NUMBER() OVER (PARTITION BY ap2.AppraisalId ORDER BY ap2.SequenceNumber) AS rn
                    FROM appraisal.AppraisalProperties ap2
                             JOIN appraisal.LandAppraisalDetails lad ON lad.AppraisalPropertyId = ap2.Id
                    WHERE lad.Province IS NOT NULL) ld ON ld.AppraisalId = a.Id AND ld.rn = 1
         OUTER APPLY (SELECT TOP 1 AppointmentDateTime
                      FROM appraisal.Appointments
                      WHERE AssignmentId = la.Id
                        AND Status != 'Cancelled'
                      ORDER BY AppointmentDateTime DESC) apt
         LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id
WHERE a.IsDeleted = 0
