-- ============================================================================
-- Collateral backfill reconciliation
--
-- Run AFTER both jobs, in this order:
--   1. POST /collateral-masters/admin/backfill                     (CollateralBackfillJob)
--   2. POST /collateral-masters/admin/backfill-host-collateral-id  (HostCollateralIdBackfillJob)
--
-- Why this file exists: CollateralBackfillJob writes a durable per-appraisal row to
-- collateral.CollateralBackfillReports, but HostCollateralIdBackfillJob reports COUNTS ONLY, in
-- memory. It tells you "23 project units unmatched" and nothing about which ones. These queries
-- recover the identities from the data.
--
-- Read the sections in order. Section 0 is the scoreboard; 1-4 name the rows behind each number.
-- Every "problem" section returns zero rows on a clean run.
-- ============================================================================

SET NOCOUNT ON;

-- ============================================================================
-- 0. Scoreboard — the numbers to report back
-- ============================================================================

WITH LegacyIds AS (
    SELECT p.AppraisalId,
           COUNT(DISTINCT h.HostCollateralId) AS DistinctIds,
           MAX(h.HostCollateralId)            AS LegacyId
    FROM (
        SELECT lad.AppraisalPropertyId AS AppraisalPropertyId, lt.HostCollateralId
        FROM appraisal.LandTitles lt
        JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
        WHERE lt.HostCollateralId IS NOT NULL
        UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.BuildingAppraisalDetails  WHERE HostCollateralId IS NOT NULL
        UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.CondoAppraisalDetails     WHERE HostCollateralId IS NOT NULL
        UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.MachineryAppraisalDetails WHERE HostCollateralId IS NOT NULL
        UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.LeaseAgreementDetails     WHERE HostCollateralId IS NOT NULL
    ) h
    JOIN appraisal.AppraisalProperties p ON p.Id = h.AppraisalPropertyId
    GROUP BY p.AppraisalId
)
SELECT 'A. legacy ids to place (ordinary)'      AS Metric, COUNT(*) AS Cnt FROM LegacyIds
-- A1/A4 reach the master THROUGH the engagement: the legacy ids are keyed by appraisal, but the
-- AS400 state itself lives on collateral.CollateralMasters (AS400 keys collateral, not appraisals).
UNION ALL SELECT 'A1. placed on a master',              COUNT(*) FROM LegacyIds l JOIN collateral.CollateralEngagements e ON e.AppraisalId = l.AppraisalId JOIN collateral.CollateralMasters m ON m.Id = e.CollateralMasterId WHERE m.HostCollateralId IS NOT NULL
UNION ALL SELECT 'A2. PROBLEM conflicting ids',         COUNT(*) FROM LegacyIds WHERE DistinctIds > 1
UNION ALL SELECT 'A3. PROBLEM no engagement',           COUNT(*) FROM LegacyIds l WHERE NOT EXISTS (SELECT 1 FROM collateral.CollateralEngagements e WHERE e.AppraisalId = l.AppraisalId)
UNION ALL SELECT 'A4. PROBLEM master id differs',       COUNT(*) FROM LegacyIds l JOIN collateral.CollateralEngagements e ON e.AppraisalId = l.AppraisalId JOIN collateral.CollateralMasters m ON m.Id = e.CollateralMasterId WHERE l.DistinctIds = 1 AND m.HostCollateralId IS NOT NULL AND m.HostCollateralId <> l.LegacyId
UNION ALL SELECT 'B. legacy ids to place (units)',      COUNT(*) FROM appraisal.ProjectUnits WHERE HostCollateralId IS NOT NULL
UNION ALL SELECT 'B1. placed on a collateral unit',     COUNT(*) FROM collateral.ProjectUnits WHERE HostCollateralId IS NOT NULL
UNION ALL SELECT 'C. masters holding an id',            COUNT(*) FROM collateral.CollateralMasters WHERE HostCollateralId IS NOT NULL
UNION ALL SELECT 'C1. PROBLEM duplicate id reuse',      COUNT(*) FROM (SELECT HostCollateralId FROM collateral.CollateralMasters WHERE HostCollateralId IS NOT NULL GROUP BY HostCollateralId HAVING COUNT(*) > 1) x
UNION ALL SELECT 'D. master backfill: not Processed',   COUNT(*) FROM collateral.CollateralBackfillReports WHERE Status <> 'Processed';

-- Expected on a clean run: every row whose Metric starts with "PROBLEM" is 0,
-- A1 = A - A2 - A3, and B1 <= B (see section 2 for why B1 is normally well below B).
--
-- Note the scoreboard buckets OVERLAP — an appraisal with conflicting ids that is also not yet
-- completed is counted in both A2 and A3. The detail query in section 1 assigns each row exactly
-- one verdict, worst-first, so its row count can be lower than A2 + A3.

-- ============================================================================
-- 1. Ordinary collateral — which appraisals did not get their id, and why
-- ============================================================================

WITH LegacyIds AS (
    SELECT p.AppraisalId,
           COUNT(DISTINCT h.HostCollateralId) AS DistinctIds,
           MAX(h.HostCollateralId)            AS LegacyId
    FROM (
        SELECT lad.AppraisalPropertyId AS AppraisalPropertyId, lt.HostCollateralId
        FROM appraisal.LandTitles lt
        JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
        WHERE lt.HostCollateralId IS NOT NULL
        UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.BuildingAppraisalDetails  WHERE HostCollateralId IS NOT NULL
        UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.CondoAppraisalDetails     WHERE HostCollateralId IS NOT NULL
        UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.MachineryAppraisalDetails WHERE HostCollateralId IS NOT NULL
        UNION ALL SELECT AppraisalPropertyId, HostCollateralId FROM appraisal.LeaseAgreementDetails     WHERE HostCollateralId IS NOT NULL
    ) h
    JOIN appraisal.AppraisalProperties p ON p.Id = h.AppraisalPropertyId
    GROUP BY p.AppraisalId
)
SELECT a.AppraisalNumber,
       a.Status                         AS AppraisalStatus,
       l.LegacyId,
       l.DistinctIds,
       m.HostCollateralId               AS OnMaster,
       r.Status                         AS MasterBackfillStatus,
       r.Message                        AS MasterBackfillMessage,
       CASE
           WHEN l.DistinctIds > 1                       THEN 'CONFLICT: property rows disagree — the job refuses to guess'
           WHEN e.AppraisalId IS NULL AND a.Status <> 'Completed'
                                                        THEN 'NO ENGAGEMENT: appraisal is not Completed, so CollateralBackfillJob skips it'
           WHEN e.AppraisalId IS NULL                   THEN 'NO ENGAGEMENT: check MasterBackfillStatus — the master upsert did not produce one'
           WHEN m.HostCollateralId IS NULL              THEN 'NOT WRITTEN: master exists but stayed NULL — re-run the host-id backfill'
           WHEN m.HostCollateralId <> l.LegacyId        THEN 'DIFFERENT: master already held another id (nightly feed wins, it is newer)'
           ELSE 'OK'
       END AS Verdict
FROM LegacyIds l
JOIN appraisal.Appraisals a ON a.Id = l.AppraisalId
LEFT JOIN collateral.CollateralEngagements e ON e.AppraisalId = l.AppraisalId
LEFT JOIN collateral.CollateralMasters m ON m.Id = e.CollateralMasterId
LEFT JOIN collateral.CollateralBackfillReports r ON r.AppraisalId = l.AppraisalId
WHERE l.DistinctIds > 1
   OR e.AppraisalId IS NULL
   OR m.HostCollateralId IS NULL
   OR m.HostCollateralId <> l.LegacyId
ORDER BY Verdict, a.AppraisalNumber;

-- ============================================================================
-- 2. Block projects — which unit ids did not land, and why
--
-- IMPORTANT: a large "not the master's last appraisal" count is NORMAL, not a fault. Only the
-- appraisal that last upserted a project owns its current unit set; ids sitting on earlier
-- appraisals in the same chain have nowhere to go and are superseded by the later ones.
-- What matters is the second bucket — units of the LAST appraisal that still failed to match.
-- ============================================================================

SELECT a.AppraisalNumber      AS ProjectAppraisalNumber,
       apu.SequenceNumber,
       apu.RoomNumber,
       apu.PlotNumber,
       apu.HostCollateralId   AS LegacyId,
       CASE
           WHEN pd.CollateralMasterId IS NULL
               THEN 'SUPERSEDED: not the master''s last appraisal — expected, the newer appraisal owns the units'
           WHEN NOT EXISTS (SELECT 1 FROM collateral.ProjectUnits cpu
                            WHERE cpu.CollateralMasterId = pd.CollateralMasterId
                              AND cpu.SequenceNumber = apu.SequenceNumber)
               THEN 'PROBLEM: no collateral unit at that sequence number'
           ELSE 'PROBLEM: sequence matches but RoomNumber/PlotNumber disagree'
       END AS Verdict
FROM appraisal.ProjectUnits apu
JOIN appraisal.Projects ap ON ap.Id = apu.ProjectId
JOIN appraisal.Appraisals a ON a.Id = ap.AppraisalId
LEFT JOIN collateral.ProjectDetails pd ON pd.LastAppraisalId = ap.AppraisalId AND pd.IsDeleted = 0
WHERE apu.HostCollateralId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM collateral.ProjectUnits cpu
      WHERE cpu.CollateralMasterId = pd.CollateralMasterId
        AND cpu.SequenceNumber = apu.SequenceNumber
        AND ISNULL(cpu.RoomNumber, N'') = ISNULL(apu.RoomNumber, N'')
        AND ISNULL(cpu.PlotNumber, N'') = ISNULL(apu.PlotNumber, N'')
        AND cpu.HostCollateralId IS NOT NULL)
ORDER BY Verdict, a.AppraisalNumber, apu.SequenceNumber;

-- ============================================================================
-- 3. One AS400 id on more than one master — always a fault
--
-- AS400 mints one id per physical collateral, and a master IS one physical collateral, so an id
-- appearing on two masters means either the legacy data attached it to unrelated collateral or the
-- dedup key let one physical thing become two masters. Unlike the old per-appraisal check, there is
-- no legitimate reason for a row here.
-- ============================================================================

SELECT m.HostCollateralId,
       COUNT(*)                              AS Masters,
       STRING_AGG(CAST(m.Id AS varchar(36)), ', ') AS MasterIds
FROM collateral.CollateralMasters m
WHERE m.HostCollateralId IS NOT NULL
GROUP BY m.HostCollateralId
HAVING COUNT(*) > 1
ORDER BY COUNT(*) DESC;

-- ============================================================================
-- 4. Master backfill failures — read this before blaming the host-id job
--
-- An appraisal with no engagement cannot receive an id. This is where you find out why.
-- ============================================================================

SELECT r.Status,
       COUNT(*) AS Cnt,
       MIN(r.Message) AS SampleMessage
FROM collateral.CollateralBackfillReports r
WHERE r.Status <> 'Processed'
GROUP BY r.Status;

SELECT TOP 100 a.AppraisalNumber, r.Status, r.Message, r.RunAt
FROM collateral.CollateralBackfillReports r
JOIN appraisal.Appraisals a ON a.Id = r.AppraisalId
WHERE r.Status <> 'Processed'
ORDER BY r.RunAt DESC;
