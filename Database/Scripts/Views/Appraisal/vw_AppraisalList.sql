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
       -- Distinct collateral type codes on this appraisal, comma-joined (e.g. 'B, L, LB').
       -- Sourced from BOTH places the type can live, because they are mutually exclusive in
       -- practice: normal appraisals carry N AppraisalProperties, while block appraisals have no
       -- AppraisalProperties at all and hold their type on the 1:1 Projects row. ProjectType codes
       -- ('U'/'LB'/'L') share the PropertyType wire format, so they union directly.
       -- This is a display aggregate — the propertyType FILTER runs the equivalent union as a
       -- semi-join (see AppraisalFilterBuilder), it does not read this column.
       -- UNION (not UNION ALL) dedupes across both sources.
       (SELECT STRING_AGG(pt.PropertyType, ', ') WITHIN GROUP (ORDER BY pt.PropertyType)
        FROM (SELECT ap3.PropertyType
              FROM appraisal.AppraisalProperties ap3
              WHERE ap3.AppraisalId = a.Id
                AND ap3.PropertyType IS NOT NULL
              UNION
              SELECT pr.ProjectType
              FROM appraisal.Projects pr
              WHERE pr.AppraisalId = a.Id
                AND pr.ProjectType IS NOT NULL) pt)                                 AS PropertyTypes,
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
       -- Company name for external assignments. The Thai name rides alongside (not instead of) the
       -- English one so the client can pick by its own locale; NULLIF collapses '' to NULL.
       comp.Name                                                                           AS CompanyName,
       NULLIF(comp.NameLocal, N'')                                                         AS CompanyNameLocal,
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
         -- "Latest active assignment", as a correlated TOP 1 rather than a
         -- ROW_NUMBER() derived table filtered by `rn = 1` on the outside.
         --
         -- The window form forced SQL Server to number EVERY non-terminal assignment row in the
         -- table before the outer WHERE could apply, because `rn = 1` sits outside the derived
         -- table and cannot be pushed in. On ~105k appraisals that produced a 14M-row Table Spool
         -- and ~11 s of CPU to return one 20-row page — regardless of how narrow the filter was.
         -- APPLY seeks IX_AppraisalAssignments_AppraisalId_AssignedAt_Active once per outer row
         -- instead. Same rows out (verified with EXCEPT both ways over the full table).
         --
         -- `la` MUST stay ahead of `comp` and `apt` below: both reference it (apt correlates on
         -- la.Id, which is projected only for that purpose and is not a view output column).
         OUTER APPLY (SELECT TOP 1 aa.Id,
                             aa.AssigneeUserId,
                             aa.AssigneeCompanyId,
                             aa.InternalAppraiserId,
                             aa.InternalAppraiserName,
                             aa.ExternalAppraiserId,
                             aa.ExternalAppraiserName,
                             aa.AssignmentType,
                             aa.AssignmentStatus,
                             aa.AssignedAt,
                             aa.SubmittedAt
                      FROM appraisal.AppraisalAssignments aa
                      WHERE aa.AppraisalId = a.Id
                        AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                      ORDER BY aa.AssignedAt DESC, aa.CreatedAt DESC, aa.Id DESC) la
         LEFT JOIN auth.Companies comp
                   ON comp.Id = TRY_CAST(la.AssigneeCompanyId AS uniqueidentifier)
         -- First property's land location. Same rewrite, same reason.
         -- IX_AppraisalProperties_AppraisalId_SequenceNumber is unique, so SequenceNumber alone
         -- already picks one row; Id is appended only so the ordering stays total if that index
         -- is ever relaxed. It costs nothing and cannot change today's result.
         OUTER APPLY (SELECT TOP 1 lad.Province,
                             lad.District,
                             lad.SubDistrict
                      FROM appraisal.AppraisalProperties ap2
                               JOIN appraisal.LandAppraisalDetails lad ON lad.AppraisalPropertyId = ap2.Id
                      WHERE ap2.AppraisalId = a.Id
                        AND lad.Province IS NOT NULL
                      ORDER BY ap2.SequenceNumber, ap2.Id) ld
         OUTER APPLY (SELECT TOP 1 AppointmentDateTime
                      FROM appraisal.Appointments
                      WHERE AssignmentId = la.Id
                        AND Status != 'Cancelled'
                      ORDER BY AppointmentDateTime DESC) apt
         LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id
WHERE a.IsDeleted = 0
