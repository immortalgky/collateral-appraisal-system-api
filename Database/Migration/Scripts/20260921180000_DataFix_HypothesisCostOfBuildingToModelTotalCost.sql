-- ============================================================
-- Data fix: carry each L&B Hypothesis's hand-built construction cost into the new
-- house model → building mapping, so no saved value moves (user decision 2026-09-21, "B").
--
-- Before: a house model's construction cost (FSD C21) was the sum of its CostOfBuilding cost
-- items (a depreciation table typed into the hypothesis itself) × the model's unit count.
-- After: C21 = HypothesisModelBuildingMappings.TotalCost, else the mapped building property's
-- Final Cost Value × unit count. The calculation no longer reads CostOfBuilding rows at all
-- (HypothesisCalculationService, Step 4).
--
-- So for every (analysis, model) that had CostOfBuilding rows, write a mapping with
--   AppraisalPropertyId = NULL  (no building chosen yet — the screen warns, and the appraiser
--                                picks one when they next open it)
--   TotalCost           = old per-house total × unit count  (the exact old C21)
-- which reproduces the old number on the next preview/save.
--
-- Per-house total mirrors the old code: SUM(COALESCE(ValueAfterDepreciation, Amount)).
-- Unit count mirrors AggregateModels: current unit rows grouped by ISNULL(ModelName, 'Unknown').
-- Models whose old total was 0 are skipped — an unmapped model already costs 0, and a stored
-- TotalCost of 0 would pin the model at 0 even after a building is chosen.
-- Models with no unit rows are skipped — they never reached the old C21 either.
--
-- The CostOfBuilding rows themselves are left alone: the next save drops them, because the
-- save handler deletes cost items missing from the payload and the screen no longer sends them.
--
-- Ordering: DbUp runs after every EF migration, so the mapping table already exists.
-- Idempotent: never inserts a mapping for a model that already has one.
-- ============================================================

;WITH legacyCost AS (
    SELECT ci.[HypothesisAnalysisId],
           LTRIM(RTRIM(ci.[ModelName])) AS [ModelName],
           SUM(COALESCE(ci.[ValueAfterDepreciation], ci.[Amount])) AS [PerUnit]
    FROM [appraisal].[HypothesisCostItems] ci
    JOIN [appraisal].[HypothesisAnalyses] ha ON ha.[Id] = ci.[HypothesisAnalysisId]
    WHERE ci.[Category] = 1          -- HypothesisCostCategory.CostOfBuilding
      AND ha.[Variant] = 1           -- HypothesisVariant.LandBuilding
      AND ci.[ModelName] IS NOT NULL
    GROUP BY ci.[HypothesisAnalysisId], LTRIM(RTRIM(ci.[ModelName]))
),
unitCount AS (
    SELECT r.[HypothesisAnalysisId],
           LTRIM(RTRIM(ISNULL(r.[ModelName], N'Unknown'))) AS [ModelName],
           COUNT(*) AS [Units]
    FROM [appraisal].[HypothesisLandBuildingUnitRows] r
    GROUP BY r.[HypothesisAnalysisId], LTRIM(RTRIM(ISNULL(r.[ModelName], N'Unknown')))
)
INSERT INTO [appraisal].[HypothesisModelBuildingMappings]
    ([Id], [HypothesisAnalysisId], [ModelName], [AppraisalPropertyId], [TotalCost], [CreatedAt], [CreatedBy])
SELECT NEWID(), lc.[HypothesisAnalysisId], lc.[ModelName], NULL,
       ROUND(lc.[PerUnit] * uc.[Units], 2), GETDATE(), N'DataFix'
FROM legacyCost lc
JOIN unitCount uc
  ON uc.[HypothesisAnalysisId] = lc.[HypothesisAnalysisId]
 AND uc.[ModelName] = lc.[ModelName]
WHERE lc.[PerUnit] <> 0
  AND NOT EXISTS (
      SELECT 1 FROM [appraisal].[HypothesisModelBuildingMappings] m
      WHERE m.[HypothesisAnalysisId] = lc.[HypothesisAnalysisId]
        AND m.[ModelName] = lc.[ModelName]);
