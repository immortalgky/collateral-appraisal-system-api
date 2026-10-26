-- Construction progress of an appraisal as a whole: one row per appraisal that has at least one
-- construction inspection. The single reader-side definition of "how far along is this appraisal" -
-- ConstructionCurrentValueService, the construction chain status query and the reporting views read it
-- instead of re-deriving the percentage from the work rows.
--
-- Per inspection it reads ConstructionInspections.SummaryPreviousProgressPct / SummaryCurrentProgressPct /
-- SummaryPreviousValue / SummaryCurrentValue, which hold the inspection-level result in BOTH modes
-- (see ConstructionInspection.RecomputeSummary), so nothing here branches on IsFullDetail.
--
-- Across inspections (same rule as ConstructionValueBreakdown):
--   weighted % = SUM(TotalValue x pct) / SUM(TotalValue) - TotalValue is only a weight, so the
--                percentages are never divided back out of rounded money.
--   no value base (SUM(TotalValue) = 0, e.g. condo units) -> the plain average of the percentages.
--   Raw*  = that figure, unrounded and unclamped. IsUnderConstruction reads it: 99.996% is not finished
--           however it displays.
--   *ProgressPct = what every caller displays: clamped to 0-100, then rounded to 2 places (halves away
--           from zero, like Math.Round(..., AwayFromZero)).
--   PreviousValue / CurrentValue = SUM of each inspection's whole-baht value, i.e. the part-built
--           buildings only - land and finished buildings are added by the caller.
--
-- Not filtered by appraisal status or by property deletion: that is the caller's decision.
CREATE OR ALTER VIEW appraisal.vw_AppraisalConstructionProgress
AS
SELECT g.AppraisalId,
       g.InspectionCount,
       g.TotalValue,
       g.PreviousValue,
       g.CurrentValue,
       g.UnweightedPreviousPercent,
       g.UnweightedCurrentPercent,
       g.WeightedPreviousPercent,
       g.WeightedCurrentPercent,
       r.RawPreviousProgressPct,
       r.RawCurrentProgressPct,
       CAST(ROUND(CASE WHEN r.RawPreviousProgressPct < 0 THEN 0
                       WHEN r.RawPreviousProgressPct > 100 THEN 100
                       ELSE r.RawPreviousProgressPct END, 2) AS decimal(9, 2)) AS PreviousProgressPct,
       CAST(ROUND(CASE WHEN r.RawCurrentProgressPct < 0 THEN 0
                       WHEN r.RawCurrentProgressPct > 100 THEN 100
                       ELSE r.RawCurrentProgressPct END, 2) AS decimal(9, 2)) AS CurrentProgressPct,
       CAST(CASE WHEN g.TotalValue > 0 THEN 1 ELSE 0 END AS bit)                AS HasValueBase,
       CAST(CASE WHEN r.RawCurrentProgressPct < 100 THEN 1 ELSE 0 END AS bit)   AS IsUnderConstruction
FROM (
    SELECT ap.AppraisalId,
           COUNT(*)                                                             AS InspectionCount,
           ISNULL(SUM(ci.TotalValue), 0)                                        AS TotalValue,
           ISNULL(SUM(ROUND(ISNULL(ci.SummaryPreviousValue, 0), 0)), 0)         AS PreviousValue,
           ISNULL(SUM(ROUND(ISNULL(ci.SummaryCurrentValue, 0), 0)), 0)          AS CurrentValue,
           -- Cast first: AVG of a decimal(7,4) keeps scale 4 and would round the average there.
           ISNULL(AVG(CAST(ISNULL(ci.SummaryPreviousProgressPct, 0) AS decimal(19, 8))), 0) AS UnweightedPreviousPercent,
           ISNULL(AVG(CAST(ISNULL(ci.SummaryCurrentProgressPct, 0)  AS decimal(19, 8))), 0) AS UnweightedCurrentPercent,
           CASE WHEN SUM(ci.TotalValue) > 0
                THEN SUM(ci.TotalValue * ISNULL(ci.SummaryPreviousProgressPct, 0)) / SUM(ci.TotalValue)
                ELSE 0 END                                                      AS WeightedPreviousPercent,
           CASE WHEN SUM(ci.TotalValue) > 0
                THEN SUM(ci.TotalValue * ISNULL(ci.SummaryCurrentProgressPct, 0)) / SUM(ci.TotalValue)
                ELSE 0 END                                                      AS WeightedCurrentPercent
    FROM appraisal.ConstructionInspections ci
    JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
    GROUP BY ap.AppraisalId
) g
CROSS APPLY (
    SELECT CASE WHEN g.TotalValue > 0 THEN g.WeightedPreviousPercent ELSE g.UnweightedPreviousPercent END AS RawPreviousProgressPct,
           CASE WHEN g.TotalValue > 0 THEN g.WeightedCurrentPercent  ELSE g.UnweightedCurrentPercent  END AS RawCurrentProgressPct
) r;
