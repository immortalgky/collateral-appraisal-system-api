-- ============================================================
-- REGULATORY_XLSX — separate destination for the regulatory Excel companion
-- Schemas: integration
--
-- The monthly regulatory job writes two files from the same row set:
--
--     REGULATORY_yyyyMMdd.txt    fixed-width 300 chars   → AS400 collects it over SFTP
--     REGULATORY_yyyyMMdd.xlsx   the same rows, readable → the Risk team opens it by hand
--
-- Both went to the single directory on the REGULATORY row, through the single outbound sink, so the
-- workbook was being uploaded to the SFTP server as well. Nobody reads it there. It belongs on the
-- Windows share the Risk team already uses, which the app-pool account can write to directly.
--
-- A second config row rather than a new column: the table already carries everything a destination
-- needs, so this costs no schema change, no EF migration, and no snapshot churn. It also buys an
-- independent FileNamePrefix and an IsActive kill switch for free.
--
-- FileInterfaceCodes.RegulatoryExcel reads this code BY NAME from C#, so per CLAUDE.md it ships as a
-- migration script and not a seeder: SeedData:RunSeeders fails closed and is true only in
-- Development, so a seeder would never reach UAT or production.
--
-- ⚠ DIRECTORY IS SEEDED TO THE DEV DEFAULT ON PURPOSE. This script runs against every database
-- including a developer's laptop, where a UNC path cannot resolve. Point it at the real share once
-- per environment after migrating — see deploy/README.md, "Regulatory Excel destination":
--
--     UPDATE integration.FileInterfaceConfigs
--     SET Directory = '\\172.20.0.14\Data_AS400\Risk\CAS'
--     WHERE InterfaceCode = 'REGULATORY_XLSX';
--
-- Until that runs the workbook simply lands next to the .txt, which is the behaviour before this
-- change. Nothing breaks if the step is forgotten; the file is just in the old place, and the job's
-- closing log line names the directory it used so that is visible in Seq without opening the DB.
--
-- Idempotent. DbUp journals the script once per database anyway.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM integration.FileInterfaceConfigs WHERE InterfaceCode = 'REGULATORY_XLSX')
BEGIN
    -- Prefix and date format match REGULATORY so the workbook keeps the file name it has today.
    -- Direction 'Out' and the NULL inbound-only columns follow the rest of the table.
    INSERT INTO integration.FileInterfaceConfigs
        (Id, InterfaceCode, Direction, FileNamePrefix, FileNameDateFormat, FileExtension,
         Directory, ProcessedDirectory, FilePattern, IsActive)
    VALUES
        (NEWID(), 'REGULATORY_XLSX', 'Out', 'REGULATORY_', 'yyyyMMdd', 'xlsx',
         './outbound', NULL, NULL, 1);

    PRINT 'Inserted FileInterfaceConfigs row for REGULATORY_XLSX (Directory = ./outbound; point it at the share per environment).';
END
ELSE
    PRINT 'FileInterfaceConfigs row for REGULATORY_XLSX already exists — left unchanged.';
GO
