-- ============================================================
-- REGULATORY_V3 — file-interface config row + recurring-job schedule (DISABLED)
-- Schemas: integration
--
-- Version 3 of the regulatory snapshot emits ONE ROW PER COLLATERAL the bank holds, taken straight
-- from the AS400 feed (collateral.HostCollateralLinks, filtered to IsMasterTitle = 1 and not
-- redeemed), and reports the date and value of that collateral's FIRST appraisal.
--
-- v1 keys on CollateralMaster and v2 on the PrevAppraisalId chain; both start from an appraisal and
-- infer which collateral it stands for. v3 stops inferring — the feed is already one row per
-- collateral with the appraisal number attached, so the row set is given. That removes the chain-tip
-- selection, the branch-point rules, and the block-project blocker in one move: the per-unit key the
-- project export was waiting on IS the AS400 collateral id.
--
-- Both the interface code (FileInterfaceCodes.RegulatoryV3) and the job id ("regulatory-export-v3",
-- in IntegrationRecurringJobs) are read BY NAME from C#, so per CLAUDE.md they ship as a migration
-- script rather than a seeder: SeedData:RunSeeders fails closed and is true only in Development, so a
-- seeder alone would never reach UAT or production.
--
-- Idempotent on both keys. DbUp journals the script once per database anyway.
--
-- The file name prefix differs from v1 and v2 on purpose — the three must never overwrite each
-- other's output in ./outbound.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM integration.FileInterfaceConfigs WHERE InterfaceCode = 'REGULATORY_V3')
BEGIN
    INSERT INTO integration.FileInterfaceConfigs
        (Id, InterfaceCode, Direction, FileNamePrefix, FileNameDateFormat, FileExtension,
         Directory, ProcessedDirectory, FilePattern, IsActive)
    VALUES
        (NEWID(), 'REGULATORY_V3', 'Out', 'REGULATORY_V3_', 'yyyyMMdd', 'txt',
         './outbound', NULL, NULL, 1);

    PRINT 'Inserted FileInterfaceConfigs row for REGULATORY_V3.';
END
ELSE
    PRINT 'FileInterfaceConfigs row for REGULATORY_V3 already exists — left unchanged.';
GO

-- The schedule row. IsEnabled = 0 is the whole point: the job is registered with Hangfire so it
-- appears in the dashboard and can be triggered by hand for comparison runs, but it does not fire on
-- its own until someone turns it on deliberately.
--
-- Cron is an hour after v2 (0 3 1 * *) so that when several are enabled they read the same state and
-- their files are comparable. It must stay behind host-collateral-link-as400 (22:00 nightly), which
-- populates the table v3 reads its row set from.
IF NOT EXISTS (SELECT 1 FROM integration.JobSchedules WHERE JobId = 'regulatory-export-v3')
BEGIN
    INSERT INTO integration.JobSchedules
        (Id, JobId, CronExpression, TimeZoneId, IsEnabled, Description)
    VALUES
        (NEWID(), 'regulatory-export-v3', '0 4 1 * *', NULL, 0,
         'Regulatory snapshot v3 — one row per AS400 collateral with its first appraisal (1st at 04:00). DISABLED until v3 output is accepted.');

    PRINT 'Inserted JobSchedules row for regulatory-export-v3 (disabled).';
END
ELSE
    PRINT 'JobSchedules row for regulatory-export-v3 already exists — left unchanged.';
GO
