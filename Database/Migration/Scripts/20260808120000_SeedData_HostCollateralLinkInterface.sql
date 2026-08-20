-- ============================================================
-- HOST_COLLATERAL_LINK file-interface config row
-- Schema: integration
--
-- The nightly AS400 COLLATLINK feed maps our AppraisalNumber to the AS400 IsMaster
-- collateral id (CCDCID). Without this row As400HostLinkJob logs
-- "No active config row for 'HOST_COLLATERAL_LINK'; skipping" and does nothing —
-- and the outbound COLLATERAL_RESULT export stays empty, because it can only emit
-- rows for appraisals that have an applied host link.
--
-- The interface code is read BY NAME from C# (FileInterfaceCodes.HostCollateralLink),
-- so per CLAUDE.md it ships as a migration script rather than relying on a seeder:
-- SeedData:RunSeeders fails closed and is true only in Development, so a seeder
-- alone would never reach UAT or production.
--
-- Idempotent: keyed on InterfaceCode, which is unique. Safe to re-run; DbUp journals
-- it once per database anyway.
--
-- Directories are relative and resolved against IHostEnvironment.ContentRootPath by
-- LocalInboundFileSource (not the process CWD — that matters under IIS). Override
-- them per environment via /admin/... or direct UPDATE if the bank's SFTP layout
-- differs; the job reads whatever this row says.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM integration.FileInterfaceConfigs WHERE InterfaceCode = 'HOST_COLLATERAL_LINK')
BEGIN
    INSERT INTO integration.FileInterfaceConfigs
        (Id, InterfaceCode, Direction, FileNamePrefix, FileNameDateFormat, FileExtension,
         [Directory], ProcessedDirectory, FilePattern, IsActive)
    VALUES
        (NEWID(),
         'HOST_COLLATERAL_LINK',
         'In',
         NULL,                        -- inbound: we match on FilePattern, not a prefix we build
         NULL,
         NULL,
         './hostlink/inbox',
         './hostlink/processed',
         'AS400_COLLATLINK_*.txt',
         1);

    PRINT 'Inserted integration.FileInterfaceConfigs row for HOST_COLLATERAL_LINK.';
END
ELSE
BEGIN
    PRINT 'integration.FileInterfaceConfigs row for HOST_COLLATERAL_LINK already exists; left unchanged.';
END
