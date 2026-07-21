-- RCAS010 — ค่าใช้จ่ายค่าประเมินที่ธนาคารจ่าย ประจำเดือน (bank-absorbed fee expense)
-- Row-level base; the RCAS010 query aggregates this (GROUP BY Channel, AssignType) with the
-- create-date filter applied BEFORE grouping, so the date filter lives in the report's SQL,
-- not the view.
CREATE
OR ALTER VIEW reporting.vw_RCAS010_FeeExpenseBase
AS
SELECT f.Id,
       f.AppraisalId,
       f.CreatedAt,
       v.Channel,
       aa.AssignmentType        AS AssignType,
       -- RM name = requestor user code + name (FSD row dimension). Fallback to the bare code.
       CASE
           WHEN v.RequestedBy IS NULL THEN NULL
           ELSE CONCAT(v.RequestedBy,
               COALESCE(N' - ' + NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(ru.FirstName, N''), N' ', NULLIF(ru.LastName, N'')))), N''), N''))
       END                      AS RmName,
       ru.Department            AS RequestorDepartment,   -- filter
       ru.AoCode                AS RequestorAoCode,        -- filter
       v.Status                 AS AppraisalStatus,        -- filter
       f.FeePaymentType,                                   -- filter (fee type)
       v.CompanyName            AS AppraisalCompany,       -- filter
       f.TotalFeeAfterVAT,
       f.CustomerPayableAmount,
       f.BankAbsorbAmount
FROM appraisal.vw_AppraisalFeeList f
         INNER JOIN appraisal.AppraisalAssignments aa ON aa.Id = f.AssignmentId
         INNER JOIN appraisal.vw_AppraisalList v ON v.Id = f.AppraisalId
         LEFT JOIN auth.AspNetUsers ru ON ru.UserName = v.RequestedBy;
