CREATE OR ALTER VIEW appraisal.vw_QuotationList AS
SELECT
    q.Id,
    q.QuotationNumber,
    q.RequestDate,
    q.DueDate,
    q.Status,
    q.RequestedByName,
    q.TotalAppraisals,
    q.TotalCompaniesInvited,
    q.TotalQuotationsReceived
FROM appraisal.QuotationRequests q
