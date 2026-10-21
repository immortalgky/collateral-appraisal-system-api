-- ============================================================
-- Backfill: the four Summary* columns of appraisal.ConstructionInspections now hold the
-- inspection-level result in BOTH modes (SummaryPreviousProgressPct / SummaryPreviousValue /
-- SummaryCurrentProgressPct / SummaryCurrentValue).
--
--   Summary mode     the percentages are keyed by the inspector and already stored; the two values
--                    were saved as 0 by the screen, so they are derived here: TotalValue x pct / 100,
--                    whole baht, halves away from zero (ConstructionMoney.ToBaht).
--   Full-detail mode all four were NULL; they are derived from the work rows, the same way
--                    ConstructionInspection.RecomputeSummary does on every save from this release on:
--                      current %  = SUM(CurrentProportionPct)
--                      previous % = ROUND(SUM(ProportionPct x PreviousProgressPct / 100), 4)
--                      values     = SUM of the rows' PreviousPropertyValue / CurrentPropertyValue
--
-- Why: every reader (ConstructionCurrentValueService, vw_AppraisalConstructionProgress, MIS, the
-- regulatory export, RCAS004) now takes progress and value from these columns instead of branching
-- on IsFullDetail. TotalValue is NOT touched - old rows keep the base they were saved with.
--
-- Ordering: DbUp runs this before the views are (re)created, so no reader sees the old columns.
-- Idempotent: the result depends only on the source columns, so a re-run writes the same values.
-- ============================================================

-- Full-detail inspections (no work rows -> 0, which is what an empty sum means).
UPDATE ci
SET    ci.SummaryCurrentProgressPct  = ISNULL(wd.CurrentPct, 0),
       ci.SummaryPreviousProgressPct = ROUND(ISNULL(wd.PreviousPct, 0), 4),
       ci.SummaryPreviousValue       = ROUND(ISNULL(wd.PreviousValue, 0), 0),
       ci.SummaryCurrentValue        = ROUND(ISNULL(wd.CurrentValue, 0), 0)
FROM   appraisal.ConstructionInspections ci
LEFT JOIN (SELECT ConstructionInspectionId,
                  SUM(CurrentProportionPct)                       AS CurrentPct,
                  SUM(ProportionPct * PreviousProgressPct / 100.0) AS PreviousPct,
                  SUM(PreviousPropertyValue)                       AS PreviousValue,
                  SUM(CurrentPropertyValue)                        AS CurrentValue
           FROM   appraisal.ConstructionWorkDetails
           GROUP BY ConstructionInspectionId) wd
       ON wd.ConstructionInspectionId = ci.Id
WHERE  ci.IsFullDetail = 1;

-- Summary-mode inspections: a percentage that was never keyed has no value either.
UPDATE appraisal.ConstructionInspections
SET    SummaryPreviousValue = CASE WHEN SummaryPreviousProgressPct IS NULL THEN NULL
                                   ELSE ROUND(TotalValue * SummaryPreviousProgressPct / 100.0, 0) END,
       SummaryCurrentValue  = CASE WHEN SummaryCurrentProgressPct IS NULL THEN NULL
                                   ELSE ROUND(TotalValue * SummaryCurrentProgressPct / 100.0, 0) END
WHERE  IsFullDetail = 0;

-- What moved, for the deployment log.
SELECT SUM(CASE WHEN IsFullDetail = 1 THEN 1 ELSE 0 END) AS FullDetailInspections,
       SUM(CASE WHEN IsFullDetail = 0 THEN 1 ELSE 0 END) AS SummaryInspections,
       SUM(CASE WHEN SummaryCurrentProgressPct IS NULL THEN 1 ELSE 0 END) AS WithoutCurrentProgress
FROM   appraisal.ConstructionInspections;
