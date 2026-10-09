-- ============================================================
-- Backfill: PricingFinalValues.FinalValueUnitType from the owning method's UnitType.
--
-- The new column records what PricingFinalValues.FinalValue / FinalValueOverride are measured in —
-- PerSqWa / PerSqm for a per-area rate, PerUnit for a whole-property lump sum. Going forward the
-- domain stamps it whenever a value is recorded (PricingAnalysisMethod.SetValue / SetFinalValue),
-- but existing rows were written before the column existed and would all read NULL, which is the
-- lump-sum reading — wrong for every market method priced per Sq.Wa.
--
-- The source is PricingAnalysisMethods.UnitType, which is exactly what consumers read off the
-- method today, so this reproduces current behaviour on old rows rather than changing any figure.
-- Nothing is computed or re-priced here.
--
-- Scope: rows whose stamp is still NULL, where the method still carries a unit. Rows whose method
-- has UnitType = NULL are left NULL on purpose — the method's calc mode was flipped
-- (SetCalcMode -> ClearValue nulls UnitType while leaving these figures standing), so the unit is
-- genuinely unknown and inventing one would be a guess. NULL reads as lump sum, which is both the
-- pre-existing behaviour for those rows and what PricingUnit.IsPerUnitRate(NULL) already returns,
-- so no consumer needs a special case.
--
-- Idempotent: a second run finds nothing to fill.
-- ============================================================

UPDATE pfv
SET pfv.[FinalValueUnitType] = pm.[UnitType]
FROM [appraisal].[PricingFinalValues] pfv
JOIN [appraisal].[PricingAnalysisMethods] pm ON pm.[Id] = pfv.[PricingMethodId]
WHERE pfv.[FinalValueUnitType] IS NULL
  AND pm.[UnitType] IS NOT NULL;
