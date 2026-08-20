-- ============================================================
-- appraisal.AS400ReportListing — placeholder for the bank's one-time legacy load
-- Schema: appraisal
--
-- WHY THIS SCRIPT EXISTS
--
-- The bank supplies this table itself: it is a one-time dump of the AS400 legacy collateral listing
-- (the "99" series), loaded out of band on U3 and production. It is deliberately NOT in the EF model
-- and no module writes to it — As400LegacyImporter and vw_RegulatoryExportV2 only read it. Same
-- situation as the appraisal.*.HostCollateralId columns reconciled by
-- 20260808120100_Schema_AppraisalHostCollateralIdColumns.sql.
--
-- vw_RegulatoryExportV2 names the table, so on any database where the bank has not loaded it —
-- a fresh clone, a CI test container, a new environment — CREATE VIEW fails with SQL error 208 and
-- the whole migration aborts. Creating it empty here keeps every environment buildable: where the
-- real table already exists this script is a no-op and the bank's own shape and data are never
-- touched; where it does not, the view compiles and the readers see zero legacy rows, which is the
-- truth on those databases.
--
-- SHAPE. The bank's DDL, with its NCHAR columns written as NVARCHAR. The placeholder is always
-- empty — it exists so CREATE VIEW resolves — so fixed-width padding has nothing to act on, and
-- NVARCHAR both matches the rest of this repo's scripts and spares the readers' RTRIM a no-op.
-- Otherwise kept in step with the copy in
-- Tests/Integration/Collateral.Integration.Tests/As400LegacyImportTests.EnsureListingTableAsync.
-- Only seven of the ten columns are read (see As400LegacyImporter.LoadListingAsync and the Legacy
-- CTE in vw_RegulatoryExportV2); the rest are carried so the placeholder and the real load are the
-- same table wherever anyone looks.
--
-- Idempotent: guarded on OBJECT_ID.
-- ============================================================

IF OBJECT_ID('appraisal.AS400ReportListing') IS NULL
BEGIN
    CREATE TABLE appraisal.AS400ReportListing
    (
        RecordType                     NVARCHAR(1)    NOT NULL,

        -- Application number of the legacy appraisal ('99A…'), char-padded — every reader RTRIMs it.
        ApplicationId                  NVARCHAR(10)   NULL,
        NewestApplicationId            NVARCHAR(10)   NULL,

        -- AS400 collateral id (CCDCID). Numeric, so the readers CAST it to bigint to drop the
        -- leading zeros before comparing it with the ids the nightly link feed lands.
        CollateralID                   DECIMAL(19, 0) NULL,

        -- 'Y' / 'N' / NULL, mapped to bit by the readers.
        UnderConstruction              NVARCHAR(1)    NULL,
        ProcessOfConstruction          DECIMAL(5, 2)  NULL,

        AppraisalValueAsCompleted      DECIMAL(15, 2) NULL,
        AppraisalValueAtTheOrigination DECIMAL(15, 2) NULL,
        ValuationDate                  DATE           NULL,
        ValuationPriceInBaht           DECIMAL(15, 2) NULL
    );
END
