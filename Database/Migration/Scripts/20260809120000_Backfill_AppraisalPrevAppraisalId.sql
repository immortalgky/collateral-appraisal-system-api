-- ============================================================
-- Backfill appraisal.Appraisals.PrevAppraisalId from request.RequestDetails.PrevAppraisalId
-- Schema: appraisal
--
-- WHY THIS SCRIPT EXISTS
--
-- PrevAppraisalId is held in two places. appraisal.Appraisals.PrevAppraisalId is a copy taken from
-- request.RequestDetails.PrevAppraisalId when the appraisal is created, so rows created before that
-- copy was introduced only carry the value on the request side.
--
-- Checked on dev, and the divergence is entirely one-directional:
--   appraisal NULL / request set   = 13 rows
--   appraisal set  / request NULL  = 0
--   both set but different         = 0
-- There is no conflict, only data that was never copied, so filling it in is safe.
--
-- WHY IT MATTERS
--
-- Both vw_RegulatoryExport and GetPreviousAppraisalChain walk the chain via
-- appraisal.Appraisals.PrevAppraisalId. Without this backfill 13 links would be missing, and the
-- regulatory report would compute the wrong "first appraisal value" — it would treat a mid-chain
-- appraisal as the chain root.
--
-- Ordering: DbUp runs before the application starts, so this lands ahead of any read.
--
-- Safe: fills only NULLs, never overwrites, and is idempotent.
-- ============================================================

DECLARE @updated int;

UPDATE a
SET    a.PrevAppraisalId = d.PrevAppraisalId
FROM   appraisal.Appraisals a
JOIN   request.RequestDetails d ON d.RequestId = a.RequestId
WHERE  a.IsDeleted = 0
  AND  a.PrevAppraisalId IS NULL
  AND  d.PrevAppraisalId IS NOT NULL;

SET @updated = @@ROWCOUNT;

PRINT 'Backfilled appraisal.Appraisals.PrevAppraisalId on ' + CAST(@updated AS varchar(20)) + ' row(s).';
