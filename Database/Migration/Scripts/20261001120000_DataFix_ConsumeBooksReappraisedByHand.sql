-- ============================================================
-- Data fix: mark AS400 reappraisal books as reviewed when a reappraisal of them was already submitted.
--
-- A book is consumed when a reappraisal request for it is submitted. Until now that happened only for
-- requests Initiate created; a reappraisal raised by hand (purpose 03 / block 09, pointing back at the
-- book's CAS appraisal) left the book Pending, so it stayed on the to-do list — and went back to "ready"
-- once that reappraisal completed. RequestSubmittedReappraisalConsumer now consumes those too; this
-- applies the same rule to books submitted before the change.
--
-- "Submitted" = the reappraisal appraisal exists (it is created on submit), not cancelled. Same rule as
-- appraisal.vw_ReappraisalsByBook arm 1, written on base tables: one-time scripts run before views.
-- Deleted rows too, like the consumer: a book skipped but reviewed anyway is reviewed. Stable with the
-- ingestor, which reopens a consumed book only when no non-cancelled reappraisal of it is left.
--
-- Not for block-project units: such a request points at the project, not at one unit.
--
-- Idempotent: a second run finds nothing to update.
-- ============================================================

UPDATE c
SET c.[Status] = 'Consumed'
FROM [collateral].[ReappraisalCandidates] c
WHERE c.[Status] IN ('Pending', 'Deleted')
  AND EXISTS (SELECT 1
              FROM [appraisal].[Appraisals] prev
              JOIN [appraisal].[Appraisals] a ON a.[PrevAppraisalId] = prev.[Id]
              WHERE prev.[AppraisalNumber] = c.[NormalizedSurveyNumber]
                AND prev.[IsDeleted] = 0
                AND a.[IsDeleted] = 0
                AND a.[Purpose] IN ('03', '09')
                AND a.[Status] <> 'Cancelled'
                -- A block project's reappraisal raised by hand cannot say which unit it reviewed.
                AND NOT EXISTS (SELECT 1 FROM [appraisal].[Projects] pj WHERE pj.[AppraisalId] = prev.[Id]));
