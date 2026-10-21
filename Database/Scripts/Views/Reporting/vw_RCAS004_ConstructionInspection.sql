-- RCAS004 — รายงานการตรวจงวดงานที่ยังไม่ครบ 100 %
-- Appraisals whose progressive construction inspection is below 100%.
-- Per-appraisal progress is read from appraisal.vw_AppraisalConstructionProgress: the properties'
-- inspections weighted by TotalValue (plain average when there is no value base), clamped to 0-100 and
-- rounded to 2 places - the same figure the application and the regulatory export report. This used
-- to be the MIN across the properties; the change is deliberate. An appraisal is listed while the
-- unrounded figure is below 100, so 99.996% still appears (as 100.00).
--
-- CODE -> DESCRIPTION RESOLUTION (FSD shows human-readable text, not stored codes):
--   * Purpose        : a.Purpose code      -> parameter.Parameters group 'AppraisalPurpose'
--   * CollateralType : ap.PropertyType     -> parameter.Parameters group 'PropertyType'
--   * InternalStaff  : AssigneeUserId username -> auth.AspNetUsers "First Last" (NormalizedUserName)
CREATE
OR ALTER VIEW reporting.vw_RCAS004_ConstructionInspection
AS
SELECT v.Id,
       v.CreatedAt              AS AppraisalCreateDate,
       v.AppraisalNumber,
       v.CustomerName,
       COALESCE(pp.Description, v.Purpose) AS Purpose,
       v.FacilityLimit          AS ApplyLimitAmount,
       ct.CollateralType,
       v.Channel,
       COALESCE(NULLIF(v.CompanyNameLocal, N''), v.CompanyName)            AS AppraisalCompany,
       COALESCE(
           NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(su.FirstName, N''), N' ', NULLIF(su.LastName, N'')))), N''),
           CAST(v.AssigneeUserId AS NVARCHAR(50))
       )                        AS InternalAppraisalStaff,
       v.AppraisalValue,
       prev.AppraisalNumber     AS PreviousAppraisalNumber,
       v.AppointmentDateTime    AS AppointmentDate,
       v.Status                 AS AppraisalStatus,
       ci.CurrentProgressPct    AS ProgressiveInspectionPct
FROM appraisal.vw_AppraisalList v
         LEFT JOIN auth.AspNetUsers su ON su.NormalizedUserName = UPPER(v.AssigneeUserId)
         OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                      WHERE [Group] = 'AppraisalPurpose' AND [Language] = 'EN' AND Code = v.Purpose) pp
         INNER JOIN appraisal.vw_AppraisalConstructionProgress ci ON ci.AppraisalId = v.Id
         LEFT JOIN appraisal.Appraisals cur ON cur.Id = v.Id
         LEFT JOIN appraisal.Appraisals prev ON prev.Id = cur.PrevAppraisalId
         OUTER APPLY (
    -- Resolve each distinct property-type code to its label before aggregating.
    SELECT CollateralType = STRING_AGG(COALESCE(cpt.Description, x.PropertyType), ', ')
    FROM (SELECT DISTINCT ap2.PropertyType
          FROM appraisal.AppraisalProperties ap2
          WHERE ap2.AppraisalId = v.Id) x
             OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                          WHERE [Group] = 'PropertyType' AND [Language] = 'EN' AND Code = x.PropertyType) cpt
    ) ct
WHERE ci.IsUnderConstruction = 1;
