-- RCAS001 — รายงานเล่มประเมินตามช่วงเวลา & ตามสถานะของงาน & ตามฝ่ายงาน
-- Appraisal books by create-date / status / department.
--
-- Layered on the core read-model vw_AppraisalList (one-way dependency: core -> report) so
-- report-specific columns never get pushed down into the shared view. Adds approach method,
-- collateral type, and approve date that the core list does not expose.
CREATE
OR ALTER VIEW reporting.vw_RCAS001_AppraisalBooks
AS
SELECT v.Id,
       v.CreatedAt                  AS AppraisalCreateDate,
       v.AppraisalNumber,
       v.CustomerName,
       v.Purpose                    AS AppraisalPurpose,
       v.FacilityLimit              AS ApplyLimitAmount,
       ct.CollateralType,
       va.ValuationApproach         AS ApproachMethod,
       v.AppraisalValue             AS AppraisalPrice,
       v.Status                     AS AppraisalStatus,
       v.RequestedBy                AS RequestorCode,
       ru.Department                AS RequestorDepartment,
       v.BankingSegment,
       v.AssigneeUserId             AS InternalAppraisalStaff,
       v.CompanyName                AS AppraisalCompany,
       a.CompletedAt                AS ApproveDate
FROM appraisal.vw_AppraisalList v
         INNER JOIN appraisal.Appraisals a ON a.Id = v.Id
         LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = v.Id
         LEFT JOIN auth.AspNetUsers ru ON ru.UserName = v.RequestedBy
         OUTER APPLY (
    SELECT CollateralType = STRING_AGG(x.PropertyType, ', ')
    FROM (SELECT DISTINCT ap.PropertyType
          FROM appraisal.AppraisalProperties ap
          WHERE ap.AppraisalId = v.Id) x
    ) ct;
