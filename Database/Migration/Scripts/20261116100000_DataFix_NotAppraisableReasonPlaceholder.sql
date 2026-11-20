-- ============================================================
-- Data fix: a building the appraiser chose not to value (IsAppraisable = 0) now needs a reason, and the form refuses to save
-- without one. Buildings saved before NotAppraisableReason existed have none, so they could not be saved again. Give them the
-- placeholder '-', the project's "nothing to say" text; the book reads '-' as no reason (PlaceholderText.Stated), so the 3.2
-- note prints no "เนื่องจาก..." for them, and the appraiser can replace it with the real reason on the next save.
--
-- appraisal.BuildingAppraisalDetails is the only table that holds IsAppraisable + NotAppraisableReason: building, land-and-
-- building and the lease variants all use it. Block / project models have neither column.
--
-- Only IsAppraisable = 0 rows with a NULL or blank reason. IsAppraisable NULL rows are NOT touched (that backfill is held
-- on purpose), and a reason someone already wrote is never overwritten. Replay-safe: after the first run no row matches.
-- ============================================================
SET NOCOUNT ON;

UPDATE appraisal.BuildingAppraisalDetails
SET    NotAppraisableReason = N'-'
WHERE  IsAppraisable = 0
  AND (NotAppraisableReason IS NULL OR LTRIM(RTRIM(NotAppraisableReason)) = N'');

SELECT @@ROWCOUNT AS NotAppraisableReasonPlaceholdersSet;
GO
