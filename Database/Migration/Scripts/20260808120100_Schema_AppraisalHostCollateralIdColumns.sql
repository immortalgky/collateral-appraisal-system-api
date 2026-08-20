-- ============================================================
-- Reconcile appraisal.*.HostCollateralId with the repository
-- Schema: appraisal
--
-- WHY THIS SCRIPT EXISTS
--
-- These five columns already exist in the live databases, where the LEGACY-SYSTEM MIGRATION
-- populated them, but they were applied out of band: they appear in no EF migration and no DbUp
-- script, and HostCollateralId appears nowhere in Modules/Appraisal source.
--
-- They are the only surviving copy of the AS400 collateral id for collateral carried over from the
-- old system. The nightly HOST_COLLATERAL_LINK feed is a delta feed and will never re-announce a
-- drawdown AS400 has already sent, so this legacy data cannot be recovered from the interface.
-- HostCollateralIdBackfillJob reads these columns and copies the ids onto the matching
-- collateral.CollateralEngagements rows.
--
-- Without this script `dotnet run --project Database/Database.csproj migrate` produces a schema that
-- lacks the columns, so the backfill job throws "Invalid column name 'HostCollateralId'" on any
-- freshly-provisioned environment while working fine against the existing ones.
--
-- NOTE ON GRAIN: the AS400 id has two grains, and these columns straddle both.
--   * Ordinary collateral — one id per APPRAISAL, while these columns are per-PROPERTY. The backfill
--     job de-duplicates per AppraisalId and skips any appraisal whose properties disagree
--     (COUNT(DISTINCT ...) > 1), reporting the count rather than guessing. The target is that
--     appraisal's collateral.CollateralEngagements row.
--   * Block projects — one id per financed UNIT, which is also the grain of ProjectUnits. AS400 mints
--     an id for each unit that has been sold and financed by the bank (unsold units never get one)
--     and stamps the PROJECT's appraisal number on all of them. The target is the matching
--     collateral.ProjectUnits row, never the project's single engagement.
--
-- NOT IN THE EF MODEL, DELIBERATELY. These are inputs from a one-time legacy migration, read only by
-- raw Dapper in HostCollateralIdBackfillJob and never by EF. Adding them to the entities would give
-- every appraisal detail a property nothing uses and that the application never writes. EF compares
-- the model against its snapshot rather than the live database, so extra columns are invisible to it
-- and will not be scaffolded away.
--
-- All 8 tables below mirror the script run against production.
--
-- Idempotent: each ALTER is guarded on COL_LENGTH.
-- ============================================================

-- 1. Land — per title deed
IF COL_LENGTH('appraisal.LandTitles', 'HostCollateralId') IS NULL
    ALTER TABLE appraisal.LandTitles ADD HostCollateralId NVARCHAR(19) NULL;

-- 2. Building
IF COL_LENGTH('appraisal.BuildingAppraisalDetails', 'HostCollateralId') IS NULL
    ALTER TABLE appraisal.BuildingAppraisalDetails ADD HostCollateralId NVARCHAR(19) NULL;

-- 3. Condo
IF COL_LENGTH('appraisal.CondoAppraisalDetails', 'HostCollateralId') IS NULL
    ALTER TABLE appraisal.CondoAppraisalDetails ADD HostCollateralId NVARCHAR(19) NULL;

-- 4. Machinery
IF COL_LENGTH('appraisal.MachineryAppraisalDetails', 'HostCollateralId') IS NULL
    ALTER TABLE appraisal.MachineryAppraisalDetails ADD HostCollateralId NVARCHAR(19) NULL;

-- 5. Vehicle — created to match production, but NOT read by the backfill job: vehicles never enter
--    the Collateral module (the in-scope types are L, LB, U, MAC, LSL, LSB, LS, PRJ), so they never
--    get a CollateralEngagement and there is nothing to write the id onto.
IF COL_LENGTH('appraisal.VehicleAppraisalDetails', 'HostCollateralId') IS NULL
    ALTER TABLE appraisal.VehicleAppraisalDetails ADD HostCollateralId NVARCHAR(19) NULL;

-- 6. Vessel — same as Vehicle: created for parity, not read by the backfill job.
IF COL_LENGTH('appraisal.VesselAppraisalDetails', 'HostCollateralId') IS NULL
    ALTER TABLE appraisal.VesselAppraisalDetails ADD HostCollateralId NVARCHAR(19) NULL;

-- 7. Lease
IF COL_LENGTH('appraisal.LeaseAgreementDetails', 'HostCollateralId') IS NULL
    ALTER TABLE appraisal.LeaseAgreementDetails ADD HostCollateralId NVARCHAR(19) NULL;

-- 8. Project units — read by the backfill, but into collateral.ProjectUnits rather than an engagement:
--    AS400 issues one id per financed unit, so a project's single engagement cannot hold them.
--    Reached via appraisal.ProjectUnits.ProjectId -> appraisal.Projects.AppraisalId, matched to the
--    collateral unit on sequence number plus room/plot. See HostCollateralIdBackfillJob, Part 2.
IF COL_LENGTH('appraisal.ProjectUnits', 'HostCollateralId') IS NULL
    ALTER TABLE appraisal.ProjectUnits ADD HostCollateralId NVARCHAR(19) NULL;
