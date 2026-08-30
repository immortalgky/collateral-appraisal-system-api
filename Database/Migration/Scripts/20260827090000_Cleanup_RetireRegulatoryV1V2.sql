-- ============================================================
-- Retire the v1 and v2 regulatory exports — v3 takes over the original names
-- Schemas: integration, collateral, hangfire
--
-- The report went through three designs. v1 keyed on CollateralMaster, v2 on the PrevAppraisalId
-- chain, v3 on the AS400 feed itself (one row per collateral the bank holds). v3 is the one that was
-- verified against the bank's own CAS RE Listing, so it becomes THE regulatory export and drops the
-- version suffix everywhere:
--
--     vw_RegulatoryExportV3  →  vw_RegulatoryExport      (the repeatable view script now carries v3's body)
--     IRegulatoryExportV3Query / RegulatoryExportV3Query / RegulatoryExportV3Job  →  unsuffixed
--     REGULATORY_V3          →  REGULATORY               (config row already exists)
--     regulatory-export-v3   →  regulatory-export        (schedule row already exists, ENABLED)
--
-- Because 'regulatory-export' is already enabled, no toggle is needed: the next run of the existing
-- schedule executes the new code and writes REGULATORY_yyyyMMdd.txt / .xlsx. This script only removes
-- what v2 and v3 left behind.
--
-- The seed scripts that created those rows (20260818160000_SeedData_RegulatoryV2JobAndInterface.sql,
-- 20260820080000_SeedData_RegulatoryV3JobAndInterface.sql) are already journalled on every database
-- and must not be edited — hence a new script that deletes forward.
--
-- ORDERING. DatabaseMigrator runs one-time scripts BEFORE repeatable ones, so the DROP VIEW below
-- happens first and the repeatable vw_RegulatoryExport.sql then CREATE OR ALTERs the surviving view
-- with v3's body. Idempotent throughout; DbUp journals it once per database anyway.
-- ============================================================

-- ── 1. Schedule rows ──────────────────────────────────────────────────────────────────────────
DELETE FROM integration.JobSchedules
WHERE JobId IN ('regulatory-export-v2', 'regulatory-export-v3');
PRINT 'Removed JobSchedules rows for regulatory-export-v2 / -v3 (if any).';
GO

-- The surviving row keeps its cron and IsEnabled untouched — only the description was written for the
-- CollateralMaster-based v1 and no longer describes what the job does.
UPDATE integration.JobSchedules
SET Description = 'Full monthly regulatory (Basel/RDT) snapshot — one row per collateral the bank holds, with that collateral''s first appraisal (1st at 02:00). Must run after host-collateral-link-as400.'
WHERE JobId = 'regulatory-export';
GO

-- ── 2. File-interface config rows ─────────────────────────────────────────────────────────────
-- REGULATORY stays: it is the row v3 now writes through, prefix 'REGULATORY_'.
DELETE FROM integration.FileInterfaceConfigs
WHERE InterfaceCode IN ('REGULATORY_V2', 'REGULATORY_V3');
PRINT 'Removed FileInterfaceConfigs rows for REGULATORY_V2 / REGULATORY_V3 (if any).';
GO

-- ── 3. The superseded views ───────────────────────────────────────────────────────────────────
-- Deleting the .sql files from the project does NOT drop anything: DbUp tracks repeatable scripts by
-- name and checksum, so a script that disappears is simply never run again and its view lingers.
DROP VIEW IF EXISTS collateral.vw_RegulatoryExportV2;
DROP VIEW IF EXISTS collateral.vw_RegulatoryExportV3;
PRINT 'Dropped vw_RegulatoryExportV2 / vw_RegulatoryExportV3 (if any).';
GO

-- ── 4. Hangfire recurring-job entries ─────────────────────────────────────────────────────────
-- RecurringJobScheduleExtensions calls RemoveIfExists only for jobs that STILL have a definition in
-- code; a row present in storage but absent from code is merely logged as a warning. With the v2/v3
-- definitions deleted, any environment where they were once enabled would keep firing them and fail
-- on a type that no longer exists. Clear them directly.
-- Guarded: Hangfire builds its own schema on application start, not through migrations, so on a
-- database that has never run the app (a fresh integration-test container, a brand-new
-- environment) these tables do not exist yet and an unguarded DELETE aborts the whole DbUp run.
-- There is nothing to clean up in that case anyway.
IF OBJECT_ID('hangfire.Hash', 'U') IS NOT NULL
    DELETE FROM hangfire.[Hash]
    WHERE [Key] IN ('recurring-job:regulatory-export-v2', 'recurring-job:regulatory-export-v3');

IF OBJECT_ID('hangfire.[Set]', 'U') IS NOT NULL
    DELETE FROM hangfire.[Set]
    WHERE [Key] = 'recurring-jobs'
      AND [Value] IN ('regulatory-export-v2', 'regulatory-export-v3');
PRINT 'Cleared Hangfire recurring-job entries for regulatory-export-v2 / -v3 (if any).';
GO
