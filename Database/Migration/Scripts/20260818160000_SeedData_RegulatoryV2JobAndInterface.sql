-- ============================================================
-- REGULATORY_V2 — file-interface config row + recurring-job schedule (DISABLED)
-- Schemas: integration
--
-- Version 2 of the regulatory snapshot is built from the appraisal chain
-- (appraisal.Appraisals.PrevAppraisalId) instead of collateral.CollateralEngagements.
-- v1 cannot report an appraisal whose CollateralMaster was never created, and 6,699
-- completed appraisals are in that position on the production-like dataset — condo
-- rows missing SubDistrict, land with no title number, leaseholds that never resolve
-- an underlying master. v2 closes that gap because it needs no master.
--
-- Both versions run side by side during the changeover. This script ships the v2
-- schedule with IsEnabled = 0: enabling it starts producing a shadow file next to
-- v1's, and v1 is switched off only once v2's output has been accepted. The choice of
-- which version is live is therefore a single UPDATE, with no deployment and no
-- ambiguity about which code produced a given file.
--
-- Both the interface code (FileInterfaceCodes.RegulatoryV2) and the job id
-- ("regulatory-export-v2", in IntegrationRecurringJobs) are read BY NAME from C#, so
-- per CLAUDE.md they ship as a migration script rather than a seeder:
-- SeedData:RunSeeders fails closed and is true only in Development, so a seeder alone
-- would never reach UAT or production.
--
-- Idempotent on both keys. DbUp journals the script once per database anyway.
--
-- The file name prefix differs from v1 on purpose — the two must never overwrite each
-- other's output in ./outbound.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM integration.FileInterfaceConfigs WHERE InterfaceCode = 'REGULATORY_V2')
BEGIN
    INSERT INTO integration.FileInterfaceConfigs
        (Id, InterfaceCode, Direction, FileNamePrefix, FileNameDateFormat, FileExtension,
         Directory, ProcessedDirectory, FilePattern, IsActive)
    VALUES
        (NEWID(), 'REGULATORY_V2', 'Out', 'REGULATORY_V2_', 'yyyyMMdd', 'txt',
         './outbound', NULL, NULL, 1);

    PRINT 'Inserted FileInterfaceConfigs row for REGULATORY_V2.';
END
ELSE
    PRINT 'FileInterfaceConfigs row for REGULATORY_V2 already exists — left unchanged.';
GO

-- The schedule row. IsEnabled = 0 is the whole point: the job is registered with Hangfire
-- so it appears in the dashboard and can be triggered by hand for comparison runs, but it
-- does not fire on its own until someone turns it on deliberately.
--
-- Cron is an hour after v1 (0 2 1 * *) so that when both are enabled they read the same
-- state and their files are comparable.
IF NOT EXISTS (SELECT 1 FROM integration.JobSchedules WHERE JobId = 'regulatory-export-v2')
BEGIN
    INSERT INTO integration.JobSchedules
        (Id, JobId, CronExpression, TimeZoneId, IsEnabled, Description)
    VALUES
        (NEWID(), 'regulatory-export-v2', '0 3 1 * *', NULL, 0,
         'Regulatory snapshot v2 — built from the appraisal chain instead of CollateralMaster (1st at 03:00). DISABLED until v2 output is accepted.');

    PRINT 'Inserted JobSchedules row for regulatory-export-v2 (disabled).';
END
ELSE
    PRINT 'JobSchedules row for regulatory-export-v2 already exists — left unchanged.';
GO
