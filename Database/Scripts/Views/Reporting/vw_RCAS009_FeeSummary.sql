-- RCAS009 — รายงานสรุปค่าประเมิน (to Accounting)
-- One row per appraisal fee, enriched with appraisal + assignment context.
CREATE
OR ALTER VIEW reporting.vw_RCAS009_FeeSummary
AS
SELECT f.Id,
       v.AppraisalNumber,
       v.CustomerName,
       aa.AssignmentType            AS AssignType,
       f.FeePaymentType             AS PayType,
       v.Purpose,
       v.CreatedAt                  AS AppraisalCreateDate,
       ct.CollateralType,
       v.Status                     AS AppraisalStatus,
       v.RequestedBy                AS RequestorCode,
       ru.Department                AS RequestorDepartment,
       v.BankingSegment,
       v.CompanyName                AS AppraisalCompany,
       v.AssigneeUserId             AS InternalAppraisalStaff,
       inv.InvoiceNumber,
       CAST(NULL AS NVARCHAR(50))   AS CostCenter,           -- not captured in the system (confirm w/ business)
       f.TotalFeeBeforeVAT          AS AppraisalFee,
       f.VATAmount                  AS VAT,
       f.TotalFeeAfterVAT           AS IncludeVAT,
       f.PaymentStatus              AS FeeStatus
FROM appraisal.vw_AppraisalFeeList f
         INNER JOIN appraisal.AppraisalAssignments aa ON aa.Id = f.AssignmentId
         INNER JOIN appraisal.vw_AppraisalList v ON v.Id = f.AppraisalId
         LEFT JOIN auth.AspNetUsers ru ON ru.UserName = v.RequestedBy
         OUTER APPLY (
    SELECT TOP 1 i.InvoiceNumber
    FROM appraisal.InvoiceItems ii
             INNER JOIN appraisal.Invoices i ON i.Id = ii.InvoiceId
    WHERE ii.AssignmentId = f.AssignmentId
    ORDER BY i.CreatedAt DESC
    ) inv
         OUTER APPLY (
    SELECT CollateralType = STRING_AGG(x.PropertyType, ', ')
    FROM (SELECT DISTINCT ap.PropertyType
          FROM appraisal.AppraisalProperties ap
          WHERE ap.AppraisalId = v.Id) x
    ) ct;
