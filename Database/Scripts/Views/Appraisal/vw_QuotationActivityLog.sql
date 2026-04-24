CREATE
OR ALTER
VIEW appraisal.vw_QuotationActivityLog AS
SELECT l.Id,
       l.QuotationRequestId,
       l.CompanyQuotationId,
       l.CompanyId,
       l.ActivityName,
       l.ActionAt,
       l.ActionBy,
       l.ActionByRole,
       l.Remark
FROM appraisal.QuotationActivityLogs l
