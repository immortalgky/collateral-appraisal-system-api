-- ============================================================
-- View: appraisal.vw_MyCompanyInvitationList
-- Purpose: Per-company, per-RFQ status surface for the vendor portal (/ext/quotations).
--          One row per (QuotationRequest, Company) pair — i.e. one row per invitation.
--
-- CompanyStatus derivation (evaluated top-to-bottom, first match wins):
--   1. RFQ cancelled                                → 'Cancelled'
--   2. Invitation declined                          → 'Declined'
--   3. Invitation expired                           → 'Expired'
--   4. No CompanyQuotation row, invitation pending  → 'Pending'
--   5. RFQ finalised, THIS company won              → 'Won'
--   6. RFQ finalised, another company won           → 'Lost'
--   7. RFQ is Negotiating AND this company is the   → 'Negotiating'
--      tentative winner (admin opened a round with them)
--   8. Otherwise                                    → CompanyQuotation.Status
--      (Submitted / UnderReview / Tentative / Negotiating /
--       Accepted / Rejected / Withdrawn / Draft / PendingCheckerReview / Declined)
--
-- Multiple CompanyQuotation rows per (RFQ, Company):
--   Possible in theory when a company submits, withdraws, and re-submits.
--   We resolve by picking the row with the latest SubmittedAt (NULLS LAST) so the
--   most-recent bid drives the display status. For rows with equal SubmittedAt (e.g.
--   both default 1900-01-01 for a Draft), we fall back to the lowest Id for stability.
-- ============================================================
CREATE OR ALTER VIEW appraisal.vw_MyCompanyInvitationList AS
SELECT
    qr.Id,
    qr.QuotationNumber,
    qr.RequestDate,
    qr.DueDate,
    qr.RequestedBy,
    qr.TotalAppraisals,
    qi.CompanyId,
    ReceivedAt    = qi.InvitedAt,
    -- Vendor-side displayed price: post-negotiation total when admin opened a round, else the original total.
    -- Null until this company has actually submitted (Draft rows carry SubmittedAt=1900-01-01 sentinel).
    TotalFeeAmount = CASE WHEN cq.SubmittedAt > '1900-01-01'
                          THEN COALESCE(cq.CurrentNegotiatedPrice, cq.TotalQuotedPrice) END,
    QuotedAt      = CASE WHEN cq.SubmittedAt > '1900-01-01' THEN cq.SubmittedAt END,
    -- Quoted By = the Maker (originator). Populated at Draft → PendingCheckerReview;
    -- legacy rows that pre-date the maker capture fall back to SubmittedByName so we don't
    -- show '—' for everything created before this column existed.
    QuotedBy      = COALESCE(cq.SubmittedToCheckerBy, cq.SubmittedByName),
    CompanyStatus = CASE
        -- 1. Whole RFQ cancelled
        WHEN qr.Status = 'Cancelled'
            THEN 'Cancelled'
        -- 2. This company's invitation is Declined
        WHEN qi.Status = 'Declined'
            THEN 'Declined'
        -- 3. This company's invitation is Expired
        WHEN qi.Status = 'Expired'
            THEN 'Expired'
        -- 4. No submitted bid yet and invitation is Pending → awaiting vendor action
        WHEN cq.Id IS NULL AND qi.Status = 'Pending'
            THEN 'Pending'
        -- 5. RFQ finalised and this company is the selected winner
        WHEN qr.Status = 'Finalized' AND qr.SelectedCompanyId = qi.CompanyId
            THEN 'Won'
        -- 6. RFQ finalised and a different company won
        WHEN qr.Status = 'Finalized' AND qr.SelectedCompanyId <> qi.CompanyId
            THEN 'Lost'
        -- 7. RFQ is Negotiating and this company is the tentative winner — admin
        --    opened a negotiation round with them. Anchored on RFQ + TentativeWinner
        --    so the badge stays correct even if CompanyQuotation.Status hasn't drifted.
        WHEN qr.Status = 'Negotiating' AND qr.TentativeWinnerQuotationId = cq.Id
            THEN 'Negotiating'
        -- 8. Use the latest CompanyQuotation.Status as-is
        ELSE cq.Status
    END,
    HasSubmitted = CAST(CASE WHEN cq.Id IS NOT NULL THEN 1 ELSE 0 END AS BIT)
FROM appraisal.QuotationRequests qr
INNER JOIN appraisal.QuotationInvitations qi
    ON qi.QuotationRequestId = qr.Id
-- Pick the most-recent CompanyQuotation for this (RFQ, Company) pair, if any.
-- OUTER APPLY returns NULL columns when no match exists (same semantic as LEFT JOIN lateral).
OUTER APPLY (
    SELECT TOP 1
        cq_inner.Id,
        cq_inner.Status,
        cq_inner.TotalQuotedPrice,
        cq_inner.CurrentNegotiatedPrice,
        cq_inner.SubmittedAt,
        cq_inner.SubmittedByName,
        cq_inner.SubmittedToCheckerBy
    FROM appraisal.CompanyQuotations cq_inner
    WHERE cq_inner.QuotationRequestId = qr.Id
      AND cq_inner.CompanyId = qi.CompanyId
    ORDER BY cq_inner.SubmittedAt DESC, cq_inner.Id ASC
) cq
-- Vendor-portal gate: only surface RFQs that have been published (Send → quotation-workflow
-- instance spawned, evidenced by at least one task row correlated to the RFQ).
-- Excludes Drafts and Draft→Cancelled RFQs that the bank never sent.
WHERE EXISTS (
    SELECT 1 FROM workflow.PendingTasks pt WHERE pt.CorrelationId = qr.Id
    UNION ALL
    SELECT 1 FROM workflow.CompletedTasks ct WHERE ct.CorrelationId = qr.Id
)
