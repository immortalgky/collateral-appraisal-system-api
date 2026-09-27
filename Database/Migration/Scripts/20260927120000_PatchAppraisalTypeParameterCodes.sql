-- Re-key the AppraisalType parameter group on the values appraisals actually store.
--
-- appraisal.Appraisals.AppraisalType (and FeeStructures, CollateralEngagements, SlaPolicies)
-- hold the names New / ReAppraisal / Progressive / PreAppraisal (AppraisalTypes.ValidValues), but
-- this group was seeded with codes '01'..'04', so nothing could look a stored value up in it — the
-- appraisal list had to carry its own name->code map to show the wording. Re-keying on the names
-- removes that map. The pairing is the one GetAppraisalResultQueryHandler.MapAppraisalType already
-- uses (New=1, ReAppraisal=2, Progressive=3, PreAppraisal=4); descriptions and SeqNo are unchanged.
--
-- Nothing reads the old codes: no code path looks this group up, and no table stores '01'..'04'
-- as an appraisal type (checked 2026-09-27).
--
-- Also fixes the TH description of the fourth row, seeded with a stray vowel ('ิBlock').
--
-- The seed script (20260317002600_SeedData_GeneralParameter.sql) is corrected in place for fresh
-- databases, but DbUp journals one-time scripts by file name with no checksum, so that edit is a
-- no-op wherever it already ran. This patch is what reaches those databases. Matching on the old
-- code makes it idempotent.

UPDATE p
SET [Code]      = c.NewCode,
    [UpdatedAt] = GETDATE(),
    [UpdatedBy] = N'SYSTEM'
FROM [parameter].[Parameters] p
JOIN (VALUES
        (N'01', N'New'),
        (N'02', N'ReAppraisal'),
        (N'03', N'Progressive'),
        (N'04', N'PreAppraisal')
     ) c (OldCode, NewCode)
  ON c.OldCode = p.[Code]
WHERE p.[Group] = N'AppraisalType';

UPDATE [parameter].[Parameters]
SET [Description] = N'Block',
    [UpdatedAt]   = GETDATE(),
    [UpdatedBy]   = N'SYSTEM'
WHERE [Group] = N'AppraisalType'
  AND [Language] = N'TH'
  AND [Code] = N'PreAppraisal'
  AND [Description] = N'ิBlock';
