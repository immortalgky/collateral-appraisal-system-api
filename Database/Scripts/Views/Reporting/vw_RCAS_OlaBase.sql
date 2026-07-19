-- Shared base view for the OLA reports (RCAS003/005/006/011). One row per appraisal with the
-- columns those reports display + RequestId (workflow correlation) and AppointmentDate, which the
-- C# OlaTimingService needs to compute the business-time OLA segments from workflow CompletedTasks.
--
-- CODE -> DESCRIPTION RESOLUTION (FSD shows human-readable text, not stored codes):
--   * Purpose        : a.Purpose code      -> parameter.Parameters group 'AppraisalPurpose'
--   * CollateralType : ap.PropertyType     -> parameter.Parameters group 'PropertyType'
--   * InternalStaff  : staff username -> auth.AspNetUsers "CODE - First Last" (NormalizedUserName)
--                      (raw code kept as InternalAppraisalStaffCode so the OLA Internal-Staff filter binds the code)
--   * Role           : that staff's Auth role(s) -> auth.AspNetUserRoles/AspNetRoles.Description
--                      (FSD "Role ผู้รับผิดชอบ" wants the description, not the internal role name)
--
-- WHICH USER IS "THE INTERNAL STAFF" depends on AssignmentType: Appraisals.AssigneeUserId is only
-- set for INTERNAL assignments (it is the int-appraisal-execution executor). On EXTERNAL assignments
-- the bank's own person is InternalAppraiserId. Picking AssigneeUserId unconditionally left this
-- column (and Role) blank for every external book.
CREATE
OR ALTER VIEW reporting.vw_RCAS_OlaBase
AS
SELECT v.Id,
       v.RequestId,
       v.CreatedAt              AS AppraisalCreateDate,
       v.AppraisalNumber,
       v.CustomerName,
       COALESCE(pp.Description, v.Purpose) AS Purpose,
       v.Purpose                AS PurposeCode,          -- raw code so the Purpose filter binds the code
       v.FacilityLimit          AS ApplyLimitAmount,
       ct.CollateralType,
       v.Channel,
       v.AssignmentType,
       v.CompanyName            AS AppraisalCompany,
       -- "CODE - First Last" per the FSD sample; falls back to the bare code when the user or the
       -- name is missing, so an unmapped/legacy code still renders.
       CASE
           WHEN sc.StaffCode IS NULL THEN NULL
           ELSE CONCAT(
               sc.StaffCode,
               COALESCE(
                   N' - ' + NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(su.FirstName, N''), N' ', NULLIF(su.LastName, N'')))), N''),
                   N''))
       END                      AS InternalAppraisalStaff,
       sc.StaffCode             AS InternalAppraisalStaffCode,
       ur.Role,
       v.RequestedBy            AS RequestorCode,
       ru.AoCode                AS RequestorAoCode,       -- for the RCAS003/011 "AO code" filter
       ru.Department            AS RequestorDepartment,   -- for the RCAS011 "Department Code" filter
       v.ExternalAppraiserId    AS ExternalStaffCode,     -- for the RCAS003 "External Appraisal Staff" filter
       v.ExternalAppraiserName  AS ExternalStaffName,
       v.BankingSegment,
       v.AppointmentDateTime    AS AppointmentDate,
       v.AssignedDate           AS AssignDate,
       v.Status                 AS AppraisalStatus
FROM appraisal.vw_AppraisalList v
         -- Resolve the staff code once; every downstream column reuses it.
         CROSS APPLY (SELECT StaffCode = CASE WHEN v.AssignmentType = 'External'
                                              THEN v.InternalAppraiserId
                                              ELSE v.AssigneeUserId END) sc
         LEFT JOIN auth.AspNetUsers su ON su.NormalizedUserName = UPPER(sc.StaffCode)
         -- Requestor org attributes (AO code, department) for the RCAS003/011 filters.
         LEFT JOIN auth.AspNetUsers ru ON ru.UserName = v.RequestedBy
         -- A user may hold more than one role, so aggregate rather than picking one.
         -- Show the role DESCRIPTION (human-readable); fall back to Name when it is blank.
         OUTER APPLY (SELECT Role = STRING_AGG(COALESCE(NULLIF(r.Description, N''), r.Name), N', ')
                      FROM auth.AspNetUserRoles aur
                               JOIN auth.AspNetRoles r ON r.Id = aur.RoleId
                      WHERE aur.UserId = su.Id) ur
         OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                      WHERE [Group] = 'AppraisalPurpose' AND [Language] = 'EN' AND Code = v.Purpose) pp
         OUTER APPLY (
    -- Resolve each distinct property-type code to its label before aggregating.
    SELECT CollateralType = STRING_AGG(COALESCE(cpt.Description, x.PropertyType), ', ')
    FROM (SELECT DISTINCT ap.PropertyType
          FROM appraisal.AppraisalProperties ap
          WHERE ap.AppraisalId = v.Id) x
             OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                          WHERE [Group] = 'PropertyType' AND [Language] = 'EN' AND Code = x.PropertyType) cpt
    ) ct;
