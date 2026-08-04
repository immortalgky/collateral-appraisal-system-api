-- ============================================================
-- Fix overlapping committee approval threshold bands
-- Schema: workflow
-- workflow.CommitteeThresholds was seeded by CommitteeDataSeed as
-- 0-10,000,000 / 10,000,000-30,000,000 / 30,000,000-NULL, so tier 1
-- overlapped tier 2 at exactly 10,000,000 and tier 2 overlapped tier 3
-- at exactly 30,000,000. Correct the two boundary columns so the bands
-- are non-overlapping, with tier 2 owning 10,000,000 through
-- 30,000,000 inclusive:
--     SUB_COMMITTEE            0.00         - 9,999,999.99
--     COMMITTEE                10,000,000   - 30,000,000
--     COMMITTEE_WITH_MEETING   30,000,000.01 - NULL
--
-- Routing is NOT affected. Committee selection at runtime reads the
-- "thresholds" array in the appraisal workflow definition JSON
-- (ApprovalMemberResolver.ResolveFromThreshold), which already encodes
-- exactly these bands; this table has no runtime reader. The workflow
-- definition and the approval-tier-switch cases are deliberately left
-- untouched, so in-flight instances are unaffected.
--
-- Idempotent: each UPDATE pins the old value in its WHERE clause, so a
-- re-run is a no-op and any already-corrected row is left alone.
-- CommitteeThresholds has no FK to Committees, so join explicitly.
-- ============================================================

DECLARE @Rows INT;

UPDATE t
SET t.MaxValue = 9999999.99
FROM workflow.CommitteeThresholds t
INNER JOIN workflow.Committees c ON c.Id = t.CommitteeId
WHERE c.Code = N'SUB_COMMITTEE'
  AND t.MaxValue = 10000000.00;

SET @Rows = @@ROWCOUNT;
IF @Rows > 0
    PRINT CONCAT('SUB_COMMITTEE MaxValue set to 9999999.99 (rows: ', @Rows, ')');
ELSE
    PRINT 'SUB_COMMITTEE MaxValue already corrected (or committee missing), skipping...';

UPDATE t
SET t.MinValue = 30000000.01
FROM workflow.CommitteeThresholds t
INNER JOIN workflow.Committees c ON c.Id = t.CommitteeId
WHERE c.Code = N'COMMITTEE_WITH_MEETING'
  AND t.MinValue = 30000000.00;

SET @Rows = @@ROWCOUNT;
IF @Rows > 0
    PRINT CONCAT('COMMITTEE_WITH_MEETING MinValue set to 30000000.01 (rows: ', @Rows, ')');
ELSE
    PRINT 'COMMITTEE_WITH_MEETING MinValue already corrected (or committee missing), skipping...';
GO

-- Verification:
-- SELECT c.Code, t.MinValue, t.MaxValue, t.Priority, t.IsActive
-- FROM workflow.CommitteeThresholds t
-- INNER JOIN workflow.Committees c ON c.Id = t.CommitteeId
-- ORDER BY t.Priority;
