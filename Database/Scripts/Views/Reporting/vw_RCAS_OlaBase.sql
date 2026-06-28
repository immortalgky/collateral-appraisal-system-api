-- Shared base view for the OLA reports (RCAS003/005/006/011). One row per appraisal with the
-- columns those reports display + RequestId (workflow correlation) and AppointmentDate, which the
-- C# OlaTimingService needs to compute the business-time OLA segments from workflow CompletedTasks.
--
-- CODE -> DESCRIPTION RESOLUTION (FSD shows human-readable text, not stored codes):
--   * Purpose        : a.Purpose code      -> parameter.Parameters group 'AppraisalPurpose'
--   * CollateralType : ap.PropertyType     -> parameter.Parameters group 'PropertyType'
--   * InternalStaff  : AssigneeUserId username -> auth.AspNetUsers "First Last" (NormalizedUserName)
--                      (raw code kept as InternalAppraisalStaffCode so the OLA Internal-Staff filter binds the code)
CREATE
OR ALTER VIEW reporting.vw_RCAS_OlaBase
AS
SELECT v.Id,
       v.RequestId,
       v.CreatedAt              AS AppraisalCreateDate,
       v.AppraisalNumber,
       v.CustomerName,
       COALESCE(pp.Description, v.Purpose) AS Purpose,
       v.FacilityLimit          AS ApplyLimitAmount,
       ct.CollateralType,
       v.Channel,
       v.AssignmentType,
       v.CompanyName            AS AppraisalCompany,
       COALESCE(
           NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(su.FirstName, N''), N' ', NULLIF(su.LastName, N'')))), N''),
           CAST(v.AssigneeUserId AS NVARCHAR(50))
       )                        AS InternalAppraisalStaff,
       v.AssigneeUserId         AS InternalAppraisalStaffCode,
       v.RequestedBy            AS RequestorCode,
       v.BankingSegment,
       v.AppointmentDateTime    AS AppointmentDate,
       v.AssignedDate           AS AssignDate,
       v.Status                 AS AppraisalStatus
FROM appraisal.vw_AppraisalList v
         LEFT JOIN auth.AspNetUsers su ON su.NormalizedUserName = UPPER(v.AssigneeUserId)
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
