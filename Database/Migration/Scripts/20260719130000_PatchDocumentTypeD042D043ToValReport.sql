-- Recategorize two report document types from VAL_DOC to VAL_REPORT:
--   D042  Construction Progress Inspection Summary Report
--   D043  Property Valuation Summary Report
--
-- The original seed (20260621090000_SeedDocumentTypesAndPatchCodes.sql) assigned VAL_DOC to the
-- whole D039-D043 range, but these two are reports (like D001 Complete Valuation Report), not
-- valuation documents. This patch corrects existing databases; on a fresh database it runs after
-- that seed and overrides D042/D043. Idempotent — the WHERE guard makes re-runs a no-op.
UPDATE [parameter].[DocumentTypes]
SET [Category]  = N'VAL_REPORT',
    [UpdatedAt] = GETDATE(),
    [UpdatedBy] = N'SYSTEM'
WHERE [Code] IN (N'D042', N'D043')
  AND ([Category] IS NULL OR [Category] <> N'VAL_REPORT');
