-- ============================================================
-- Backfill: ReappraisalCandidates.NormalizedSurveyNumber and LastSeenFileDate.
--
-- COLLATREV candidates are now one row per BOOK (CollateralId + normalised survey number) across
-- every monthly file, because AS400 repeats the books still on its due list each month. The ingestor
-- writes both new columns on every row it creates or refreshes; this fills them on rows written
-- before the columns existed.
--
--   NormalizedSurveyNumber — SurveyNumber with AS400's block-project 'B' prefix dropped, the same
--                            rule as As400AppraisalNumber.Normalize and
--                            collateral.vw_HostCollateralLinkKeys.CasAppraisalNumber.
--   LastSeenFileDate       — the file the row came from. Rows from before the change are one per
--                            file, so this is exactly their SourceFileDate.
--
-- Readers already fall back to these values when the columns are NULL, so this changes no figure;
-- it lets the join key be read straight off the index. The last statement also consumes stale Pending
-- copies of books already reviewed (see there).
--
-- Idempotent: a second run finds nothing to fill.
-- ============================================================

UPDATE rc
SET rc.[NormalizedSurveyNumber] = UPPER(CASE
        WHEN LEFT(LTRIM(RTRIM(rc.[SurveyNumber])), 1) = 'B' AND LEN(LTRIM(RTRIM(rc.[SurveyNumber]))) > 1
            THEN SUBSTRING(LTRIM(RTRIM(rc.[SurveyNumber])), 2, LEN(LTRIM(RTRIM(rc.[SurveyNumber]))))
        ELSE LTRIM(RTRIM(rc.[SurveyNumber]))
    END)
FROM [collateral].[ReappraisalCandidates] rc
WHERE rc.[NormalizedSurveyNumber] IS NULL;

UPDATE rc
SET rc.[LastSeenFileDate] = rc.[SourceFileDate]
FROM [collateral].[ReappraisalCandidates] rc
WHERE rc.[LastSeenFileDate] IS NULL;

-- Before books were deduplicated every monthly file added a row, and Initiate consumed only the row it
-- was run on. A later month's copy of a book already reviewed is still Pending and on the latest file,
-- so it would show as to-do. It is the same book: consume the copies. (The ingestor never creates such
-- copies again; a copy whose reappraisal was later cancelled is reopened by the next file as usual.)
-- By book alone, like consumption on submit: one request covers every collateral of the book.
-- Deleted copies too, as the submit consumer does: under another collateral the ingestor never sees
-- the Consumed row, so a Deleted copy would stay refreshable and a restore would re-open a reviewed book.
-- Not across collateral for a block-project number (or a unit ticket): there each collateral is a
-- different unit of the project, reviewed on its own.
UPDATE p
SET p.[Status] = 'Consumed'
FROM [collateral].[ReappraisalCandidates] p
CROSS APPLY (SELECT CAST(CASE
                 WHEN LEN(p.[NormalizedSurveyNumber]) = 8 AND SUBSTRING(p.[NormalizedSurveyNumber], 3, 1) = 'U' THEN 1
                 WHEN EXISTS (SELECT 1
                              FROM [appraisal].[Appraisals] a
                              JOIN [appraisal].[Projects] pj ON pj.[AppraisalId] = a.[Id]
                              WHERE a.[AppraisalNumber] = p.[NormalizedSurveyNumber] AND a.[IsDeleted] = 0) THEN 1
                 ELSE 0 END AS bit) AS IsUnit) u
WHERE p.[Status] IN ('Pending', 'Deleted')
  AND EXISTS (SELECT 1
              FROM [collateral].[ReappraisalCandidates] c
              WHERE c.[NormalizedSurveyNumber] = p.[NormalizedSurveyNumber]
                AND c.[Status] = 'Consumed'
                AND c.[Id] <> p.[Id]
                AND (u.IsUnit = 0 OR c.[CollateralId] = p.[CollateralId]));

