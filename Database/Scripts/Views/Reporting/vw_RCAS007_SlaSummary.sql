-- RCAS007 — รายงานสรุปการทำงานตาม SLA ของทั้ง Internal & External
-- One row per appraisal report. The SLA value (business-day elapsed) is NOT computed here — business
-- hours need the holiday/lunch calendar, so it is computed in C# (IReportSlaCalculator) from
-- AppointmentDate → SubmittedAt (or now when not yet submitted). This view only projects the source
-- fields + SubmittedAt/AppointmentDate the calculator needs.
--
-- "Internal Appraisal Staff" picks by AssignmentType (External → InternalAppraiserId, else
-- AssigneeUserId) — same rule as vw_RCAS_OlaBase. Role = that staff's Auth role(s). Also shared with
-- RCAS012 (which filters SLA > 2 days on top of this).
CREATE
OR ALTER VIEW reporting.vw_RCAS007_SlaSummary
AS
SELECT v.Id,
       v.RequestId,
       v.AppraisalNumber,
       v.CustomerName,
       COALESCE(pp.Description, v.Purpose) AS Purpose,
       v.Purpose                AS PurposeCode,
       v.RequestedBy            AS RequestorCode,
       COALESCE(
           NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(ru.FirstName, N''), N' ', NULLIF(ru.LastName, N'')))), N''),
           v.RequestedBy)       AS RequestorName,
       ru.PhoneNumber           AS RequestorPhone,
       ru.Department            AS RequestorDepartment,
       v.BankingSegment,
       v.CompanyName            AS AppraisalCompany,
       v.ExternalAppraiserName  AS ExternalStaffName,
       v.ExternalAppraiserId    AS ExternalStaffCode,     -- filter-only: the External Appraisal Staff filter binds the code
       v.AssignmentType,                                  -- filter-only: RCAS012 scopes to External
       comp.Phone               AS AppraisalCompanyPhone,
       -- "CODE - First Last" (same shape as vw_RCAS_OlaBase); fallback to the bare code.
       CASE
           WHEN sc.StaffCode IS NULL THEN NULL
           ELSE CONCAT(sc.StaffCode,
               COALESCE(N' - ' + NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(su.FirstName, N''), N' ', NULLIF(su.LastName, N'')))), N''), N''))
       END                      AS InternalAppraisalStaff,
       sc.StaffCode             AS InternalAppraisalStaffCode,
       su.PhoneNumber           AS InternalAppraisalStaffPhone,
       fee.TotalFeeAfterVAT     AS AppraisalFee,
       v.CreatedAt              AS AppraisalCreateDate,
       v.AppointmentDateTime    AS AppointmentDate,
       v.SubmittedAt,                                     -- SLA end-point (null → still in progress)
       v.AppraisalValue,
       ur.Role,                                           -- FSD "Current Role" = the internal staff's Auth role(s)
       v.Status                 AS AppraisalStatus
FROM appraisal.vw_AppraisalList v
         CROSS APPLY (SELECT StaffCode = CASE WHEN v.AssignmentType = 'External'
                                              THEN v.InternalAppraiserId ELSE v.AssigneeUserId END) sc
         LEFT JOIN auth.AspNetUsers su ON su.NormalizedUserName = UPPER(sc.StaffCode)
         LEFT JOIN auth.AspNetUsers ru ON ru.UserName = v.RequestedBy
         LEFT JOIN auth.Companies comp ON comp.Id = TRY_CAST(v.AssigneeCompanyId AS uniqueidentifier)
         -- Show the role DESCRIPTION (human-readable); fall back to Name when it is blank.
         OUTER APPLY (SELECT Role = STRING_AGG(COALESCE(NULLIF(r.Description, N''), r.Name), N', ')
                      FROM auth.AspNetUserRoles aur
                               JOIN auth.AspNetRoles r ON r.Id = aur.RoleId
                      WHERE aur.UserId = su.Id) ur
         OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                      WHERE [Group] = 'AppraisalPurpose' AND [Language] = 'EN' AND Code = v.Purpose) pp
         OUTER APPLY (SELECT TOP 1 f.TotalFeeAfterVAT
                      FROM appraisal.vw_AppraisalFeeList f
                      WHERE f.AppraisalId = v.Id) fee;
