-- RCAS001 — รายงานเล่มประเมินตามช่วงเวลา & ตามสถานะของงาน & ตามฝ่ายงาน
-- Appraisal books by create-date / status / department.
--
-- Layered on the core read-model vw_AppraisalList (one-way dependency: core -> report) so
-- report-specific columns never get pushed down into the shared view. Adds approach method,
-- collateral type, and approve date that the core list does not expose.
--
-- CODE -> DESCRIPTION RESOLUTION (FSD shows human-readable text, not the stored codes):
--   * AppraisalPurpose : a.Purpose code (e.g. '01')        -> parameter.Parameters group 'AppraisalPurpose'
--   * CollateralType   : ap.PropertyType code (e.g. 'LB')  -> parameter.Parameters group 'PropertyType'
--   * ApproachMethod   : va.ValuationApproach code ('WQS') -> parameter.Parameters group 'ApproachMethod'
--   * InternalStaff    : AssigneeUserId is an internal username -> auth.AspNetUsers "First Last"
-- Each lookup is COALESCE(description, raw code) so an unmapped/legacy code still renders.
-- Matches the resolution pattern already used in workflow.vw_TaskMonitor.
CREATE
OR ALTER VIEW reporting.vw_RCAS001_AppraisalBooks
AS
SELECT v.Id,
       v.CreatedAt                              AS AppraisalCreateDate,
       v.AppraisalNumber,
       v.CustomerName,
       COALESCE(pp.Description, v.Purpose)      AS AppraisalPurpose,
       v.FacilityLimit                          AS ApplyLimitAmount,
       ct.CollateralType,
       COALESCE(pm.Description, va.ValuationApproach) AS ApproachMethod,
       v.AppraisalValue                         AS AppraisalPrice,
       v.Status                                 AS AppraisalStatus,
       v.RequestedBy                            AS RequestorCode,
       ru.Department                            AS RequestorDepartment,
       v.BankingSegment,
       COALESCE(
           NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(su.FirstName, N''), N' ', NULLIF(su.LastName, N'')))), N''),
           CAST(v.AssigneeUserId AS NVARCHAR(50))
       )                                        AS InternalAppraisalStaff,
       v.CompanyName                            AS AppraisalCompany,
       a.CompletedAt                            AS ApproveDate
FROM appraisal.vw_AppraisalList v
         INNER JOIN appraisal.Appraisals a ON a.Id = v.Id
         LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = v.Id
         LEFT JOIN auth.AspNetUsers ru ON ru.UserName = v.RequestedBy
         LEFT JOIN auth.AspNetUsers su ON su.NormalizedUserName = UPPER(v.AssigneeUserId)
         OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                      WHERE [Group] = 'AppraisalPurpose' AND [Language] = 'EN' AND Code = v.Purpose) pp
         OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                      WHERE [Group] = 'ApproachMethod' AND [Language] = 'EN' AND Code = va.ValuationApproach) pm
         OUTER APPLY (
    -- Resolve each distinct property-type code to its label before aggregating.
    SELECT CollateralType = STRING_AGG(COALESCE(cpt.Description, x.PropertyType), ', ')
    FROM (SELECT DISTINCT ap.PropertyType
          FROM appraisal.AppraisalProperties ap
          WHERE ap.AppraisalId = v.Id) x
             OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                          WHERE [Group] = 'PropertyType' AND [Language] = 'EN' AND Code = x.PropertyType) cpt
    ) ct;
