-- ============================================================
-- Data fix: Income-approach IndicatedValue — 0 meant "no override" under the old ">0" gate;
-- clear it to NULL so it means "no override" under the new null-based rule too.
--
-- SaveIncomeAnalysisCommandHandler used to test `AppraisalPriceRounded is > 0` to decide whether
-- the appraiser had overridden the method value, treating a stored 0 as "not set". The column
-- (renamed AppraisalPriceRounded -> PricingFinalValues.IndicatedValue) is now read null-based,
-- matching every other pricing method: IndicatedValue ?? FinalValue. Left untouched, an existing
-- stored 0 would become a real override and zero out the method value on next load.
--
-- Scoped to Income-approach methods only — other method types never wrote a bare 0 under the old
-- rule, and their IndicatedValue already means "no override" when NULL.
--
-- Ordering: DbUp runs after every EF migration, so PricingFinalValues.IndicatedValue already exists.
-- Idempotent: only rows still holding the stale 0 are touched.
-- ============================================================

UPDATE pfv
SET pfv.[IndicatedValue] = NULL
FROM [appraisal].[PricingFinalValues] pfv
JOIN [appraisal].[PricingAnalysisMethods] pam ON pam.[Id] = pfv.[PricingMethodId]
WHERE pam.[MethodType] = 'Income'
  AND pfv.[IndicatedValue] = 0;
