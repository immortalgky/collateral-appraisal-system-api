-- Sort BoundaryMarker 'อื่นๆ' (code 99) last.
--
-- 20260317002600_SeedData_GeneralParameter.sql gave it SeqNo 4, ahead of 'หลักเขตชำรุด' (SeqNo 5),
-- so the land title dialog listed "Other" before a real option. SeqNo 99 matches its code and
-- leaves room for any option added later.
--
-- The seed is corrected in place for fresh databases; this reaches databases where it already ran.
-- Matching the old SeqNo keeps it idempotent and leaves an order an administrator has since set alone.

UPDATE [parameter].[Parameters]
SET [SeqNo]     = 99,
    [UpdatedAt] = GETDATE(),
    [UpdatedBy] = N'SYSTEM'
WHERE [Group] = N'BoundaryMarker'
  AND [Code]  = N'99'
  AND [SeqNo] = 4;

PRINT CONCAT('Moved BoundaryMarker ''Other'' to the end: ', @@ROWCOUNT, ' row(s).');
