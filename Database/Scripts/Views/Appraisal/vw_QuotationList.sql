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
    q.TotalQuotationsReceived,
    q.RmUserId
    -- v2: AppraisalId column dropped; use QuotationRequestAppraisals join table for filtering.
    -- TotalAppraisals reflects the join table row count (denormalised counter updated by domain methods).
    -- v4: FinalizedAckedAt dropped (AckFinalizedQuotation workflow task removed).
FROM appraisal.QuotationRequests q
