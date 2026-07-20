-- RCAS009 — รายงานสรุปค่าประเมิน (to Accounting)
-- One row per appraisal fee, enriched with appraisal + assignment context.
--
-- CODE -> DESCRIPTION RESOLUTION (FSD shows human-readable text, not stored codes):
--   * Purpose        : a.Purpose code        -> parameter.Parameters group 'AppraisalPurpose'
--   * CollateralType : ap.PropertyType        -> parameter.Parameters group 'PropertyType'
--   * PayType        : f.FeePaymentType ('04') -> parameter.Parameters group 'FeePaymentMethod'
--                      (raw code kept as PayTypeCode so the report's Pay-Type filter still binds the code)
--   * InternalStaff  : AssigneeUserId username -> auth.AspNetUsers "First Last" (NormalizedUserName)
-- AssignType (Internal/External) and FeeStatus (NotPaid/Partial/PendingInvoice/Paid) are already
-- stored as readable text, so they are passed through unchanged.
CREATE
OR ALTER VIEW reporting.vw_RCAS009_FeeSummary
AS
SELECT f.Id,
       v.AppraisalNumber,
       v.CustomerName,
       aa.AssignmentType            AS AssignType,
       COALESCE(pf.Description, f.FeePaymentType) AS PayType,
       f.FeePaymentType                           AS PayTypeCode,
       COALESCE(pp.Description, v.Purpose)        AS Purpose,
       v.CreatedAt                  AS AppraisalCreateDate,
       ct.CollateralType,
       v.Status                     AS AppraisalStatus,
       v.RequestedBy                AS RequestorCode,
       ru.Department                AS RequestorDepartment,
       v.BankingSegment,
       v.CompanyName                AS AppraisalCompany,
       COALESCE(
           NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(su.FirstName, N''), N' ', NULLIF(su.LastName, N'')))), N''),
           CAST(v.AssigneeUserId AS NVARCHAR(50))
       )                            AS InternalAppraisalStaff,
       inv.InvoiceNumber,
       -- Cost Center of the REQUESTOR: AspNetUsers.AoCode -> Officers.OfficerCode -> CostCenterCode
       -- -> CostCenters.Description. NOTE a known 8-digit(SSONTH) vs 3-digit(G7CNTR) code mismatch
       -- means the CostCenters join often finds no description; fall back to the raw code.
       COALESCE(rcc.Description, rof.CostCenterCode) AS CostCenter,
       f.TotalFeeBeforeVAT          AS AppraisalFee,
       f.VATAmount                  AS VAT,
       f.TotalFeeAfterVAT           AS IncludeVAT,
       f.PaymentStatus              AS FeeStatus
FROM appraisal.vw_AppraisalFeeList f
         INNER JOIN appraisal.AppraisalAssignments aa ON aa.Id = f.AssignmentId
         INNER JOIN appraisal.vw_AppraisalList v ON v.Id = f.AppraisalId
         LEFT JOIN auth.AspNetUsers ru ON ru.UserName = v.RequestedBy
         LEFT JOIN auth.Officers rof ON rof.OfficerCode = ru.AoCode
         LEFT JOIN auth.CostCenters rcc ON rcc.Code = rof.CostCenterCode
         LEFT JOIN auth.AspNetUsers su ON su.NormalizedUserName = UPPER(v.AssigneeUserId)
         OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                      WHERE [Group] = 'AppraisalPurpose' AND [Language] = 'EN' AND Code = v.Purpose) pp
         OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                      WHERE [Group] = 'FeePaymentMethod' AND [Language] = 'EN' AND Code = f.FeePaymentType) pf
         OUTER APPLY (
    SELECT TOP 1 i.InvoiceNumber
    FROM appraisal.InvoiceItems ii
             INNER JOIN appraisal.Invoices i ON i.Id = ii.InvoiceId
    WHERE ii.AssignmentId = f.AssignmentId
    ORDER BY i.CreatedAt DESC
    ) inv
         OUTER APPLY (
    -- Resolve each distinct property-type code to its label before aggregating.
    SELECT CollateralType = STRING_AGG(COALESCE(cpt.Description, x.PropertyType), ', ')
    FROM (SELECT DISTINCT ap.PropertyType
          FROM appraisal.AppraisalProperties ap
          WHERE ap.AppraisalId = v.Id) x
             OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                          WHERE [Group] = 'PropertyType' AND [Language] = 'EN' AND Code = x.PropertyType) cpt
    ) ct;
