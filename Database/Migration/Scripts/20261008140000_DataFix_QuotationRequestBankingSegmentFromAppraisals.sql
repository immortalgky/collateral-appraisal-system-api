/*
  20261008140000_DataFix_QuotationRequestBankingSegmentFromAppraisals.sql

  Purpose : Recompute appraisal.QuotationRequests.BankingSegment (the cached Segment Set JSON
            array) for every row, from the Banking Segments of the appraisals currently on each
            quotation — clearing whatever is stored today and reassigning it from source data.

  Why     : The column used to be a single legacy value, stamped once at creation and never kept
            in sync as appraisals were added or removed. The application now maintains it on every
            appraisal add/remove (QuotationRequest.SetBankingSegment), but that only covers writes
            going forward — rows saved before that logic existed, or saved through handler bugs
            fixed later in the same release (RemoveAppraisalFromDraft not clearing the cache on the
            last-appraisal auto-cancel case; SendQuotation's coverage check querying company IDs
            instead of appraisal IDs), can still carry a stale, wrong, or empty value. This script
            brings every existing row to the same correct state the application now maintains.

  Rule    : Per QuotationRequest, the set is every distinct, non-blank appraisal.Appraisals.
            BankingSegment for appraisals currently in appraisal.QuotationRequestAppraisals,
            deduplicated case-insensitively, encoded as a JSON array. A quotation with no
            appraisals, or whose appraisals all have a null/blank segment, gets '[]'.

  Safety  : Idempotent — recomputes and overwrites every row the same way on every run; running it
            twice in a row changes nothing the second time. Read-only on everything except this one
            column; does not touch Status, TotalAppraisals, or any appraisal/company data.
*/

SET NOCOUNT ON;

;WITH SegmentSets AS (
    SELECT
        qra.QuotationRequestId,
        (
            SELECT '[' + STRING_AGG('"' + REPLACE(seg.Segment, '"', '\"') + '"', ',') + ']'
            FROM (
                SELECT DISTINCT a.BankingSegment AS Segment
                FROM appraisal.QuotationRequestAppraisals qra2
                JOIN appraisal.Appraisals a ON a.Id = qra2.AppraisalId
                WHERE qra2.QuotationRequestId = qra.QuotationRequestId
                  AND a.BankingSegment IS NOT NULL
                  AND LTRIM(RTRIM(a.BankingSegment)) <> ''
            ) seg
        ) AS SegmentJson
    FROM appraisal.QuotationRequestAppraisals qra
    GROUP BY qra.QuotationRequestId
)
UPDATE q
SET q.BankingSegment = COALESCE(s.SegmentJson, '[]')
FROM appraisal.QuotationRequests q
LEFT JOIN SegmentSets s ON s.QuotationRequestId = q.Id
WHERE q.BankingSegment <> COALESCE(s.SegmentJson, '[]');

PRINT CONCAT('Recomputed BankingSegment for ', @@ROWCOUNT, ' QuotationRequest row(s).');
