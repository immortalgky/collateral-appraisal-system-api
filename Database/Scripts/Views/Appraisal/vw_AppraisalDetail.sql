CREATE
OR ALTER
VIEW appraisal.vw_AppraisalDetail AS
SELECT a.Id,
       a.AppraisalNumber,
       a.RequestId,
       a.RequestedAt,
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
       a.SLAHours,
       a.SLADueDate,
       a.SLAStatus,
       CAST(a.SLAHours AS DECIMAL(9,2)) / 8.0                                               AS SLABusinessDays,
       a.ActualHoursToComplete,
       a.IsWithinSLA,
       (SELECT COUNT(*) FROM appraisal.AppraisalProperties ap WHERE ap.AppraisalId = a.Id)  AS PropertyCount,
       (SELECT COUNT(*) FROM appraisal.PropertyGroups pg WHERE pg.AppraisalId = a.Id)       AS GroupCount,
       (SELECT COUNT(*) FROM appraisal.AppraisalAssignments aa WHERE aa.AppraisalId = a.Id) AS AssignmentCount,
       a.CreatedAt,
       a.CreatedBy,
       a.UpdatedAt,
       a.UpdatedBy,
       -- Block appraisal detection: presence of a Projects row is authoritative
       CAST(CASE WHEN p.Id IS NOT NULL THEN 1 ELSE 0 END AS BIT)                           AS IsBlock,
       -- Column is already the parameter-table code ("U"/"LB"/"L"); no translation needed.
       p.ProjectType                                                                       AS BlockProjectType,
       -- Appraiser Information section on the 360 page.
       -- Company: bank-counterparty for external assignments only; NULL for internal (FE renders "-").
       -- Appraiser: bank-side staff responsible — InternalAppraiserId for external assignments
       --            (the internal follow-up staff), AssigneeUserId for internal assignments.
       -- AppraisalDate: latest non-Cancelled appointment, then appraisal.CompletedAt, then assignment.CompletedAt.
       c.Name                                                                              AS CompanyName,
       NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(u.FirstName, N''), N' ', NULLIF(u.LastName, N'')))), N'')
                                                                                           AS AppraiserName,
       COALESCE(lap.AppointmentDateTime, a.CompletedAt, la.CompletedAt)                    AS AppraisalDate,
       -- Committee approval tier + meeting linkage. Tier derives from the approving committee
       -- CODE stored on the appraisal (1 = sub-committee, 2 = committee without meeting,
       -- 3 = committee with meeting) — robust even when the Committees table lacks that code.
       -- Meeting number/date come from the linked meeting on the outcome row.
       CASE a.ApprovedByCommittee
           WHEN 'SUB_COMMITTEE' THEN 1
           WHEN 'COMMITTEE' THEN 2
           WHEN 'COMMITTEE_WITH_MEETING' THEN 3
       END                                                                                 AS ApprovalTier,
       CASE a.ApprovedByCommittee
           WHEN 'SUB_COMMITTEE' THEN 'Sub-Committee'
           WHEN 'COMMITTEE' THEN 'Committee'
           WHEN 'COMMITTEE_WITH_MEETING' THEN 'Committee (Meeting)'
       END                                                                                 AS ApprovalTierLabel,
       rv.MeetingId                                                                        AS MeetingId,
       rvm.EndAt                                                                           AS MeetingDate,
       rvm.MeetingNo                                                                       AS MeetingReference
FROM appraisal.Appraisals a
         OUTER APPLY (SELECT TOP 1 pt.ActivityId
                      FROM workflow.PendingTasks pt
                      WHERE pt.CorrelationId = a.RequestId
                      ORDER BY pt.AssignedAt DESC) wpt
         OUTER APPLY (SELECT TOP 1 ip.Id, ip.ProjectType
                      FROM appraisal.Projects ip
                      WHERE ip.AppraisalId = a.Id) p
         OUTER APPLY (SELECT TOP 1 aa.Id,
                                   aa.AssigneeCompanyId,
                                   aa.AssigneeUserId,
                                   aa.InternalAppraiserId,
                                   aa.CompletedAt
                      FROM appraisal.AppraisalAssignments aa
                      WHERE aa.AppraisalId = a.Id
                        AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                      ORDER BY aa.AssignedAt DESC) la
         OUTER APPLY (SELECT TOP 1 ap.AppointmentDateTime
                      FROM appraisal.Appointments ap
                      WHERE ap.AssignmentId = la.Id
                        AND ap.Status <> 'Cancelled'
                      ORDER BY ap.AppointmentDateTime DESC) lap
         -- The committee-approval outcome row (one per appraisal) carries the linked meeting.
         OUTER APPLY (SELECT TOP 1 ar.MeetingId
                      FROM appraisal.AppraisalReviews ar
                      WHERE ar.AppraisalId = a.Id) rv
         -- Meeting number/date resolved from the linked meeting.
         LEFT JOIN workflow.Meetings rvm ON rvm.Id = rv.MeetingId
         LEFT JOIN auth.Companies c
                ON c.Id = TRY_CAST(la.AssigneeCompanyId AS UNIQUEIDENTIFIER)
               AND c.IsDeleted = 0
         LEFT JOIN auth.AspNetUsers u
                ON u.Id = TRY_CAST(CASE WHEN la.AssigneeCompanyId IS NOT NULL
                                        THEN la.InternalAppraiserId
                                        ELSE la.AssigneeUserId
                                   END AS UNIQUEIDENTIFIER)
