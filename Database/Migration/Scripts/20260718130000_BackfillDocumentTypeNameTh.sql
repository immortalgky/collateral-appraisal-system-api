-- =============================================================================
-- Backfill parameter.DocumentTypes.NameTh from the general Parameter seed
--
-- Background: parameter.DocumentTypes was made bilingual by adding a NameTh
-- column (EF migration AddDocumentTypeNameTh). This script populates it from
-- the existing 'DocumentType' Parameters group, which already carries both
-- EN and TH descriptions per code (see 20260317002600_SeedData_GeneralParameter.sql
-- and 20260621090000_SeedDocumentTypesAndPatchCodes.sql, which seeded .Name
-- from the EN rows of the same group).
--
-- Only rows where NameTh IS NULL are touched, so this is safe to re-run and
-- will not overwrite a value set manually via the admin UI afterwards.
-- =============================================================================

UPDATE dt
SET dt.[NameTh] = p.[description]
FROM parameter.DocumentTypes dt
INNER JOIN parameter.Parameters p
    ON p.[group] = N'DocumentType'
   AND p.[language] = N'TH'
   AND p.[code] = dt.[Code]
WHERE dt.[NameTh] IS NULL;
GO
