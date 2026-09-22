-- ============================================================
-- Data fix: un-pin Leasehold / ProfitRent methods whose IndicatedValue was stamped, not typed.
--
-- The old SaveLeaseholdAnalysis / SaveProfitRentAnalysis handlers wrote
-- `SetAppraisalPrice(command.AppraisalPrice ?? finalPrice)` on EVERY save, so the column now named
-- PricingFinalValues.IndicatedValue holds the computed figure even when the appraiser never typed
-- over anything. The handlers no longer do that, but the stamped rows remain — and IndicatedValue
-- is "the appraiser's typed-over total": SyncMethodValueWithIndicatedValue pins MethodValue to it,
-- so later recalculations (rent schedule changes, the corrected partial-usage rate) would never
-- reach the rollup, the book or LOS. 20260919160000 only fills IndicatedValue where it is NULL, so
-- it does not touch these rows.
--
-- Scope — the UNAMBIGUOUS case only: IndicatedValue = FinalValue, excluding partial-usage Leasehold
-- (see the WHERE clause). Clearing it there changes no figure anywhere today (every reader
-- resolves IndicatedValue ?? FinalValue, and MethodValue is left as stored); it only stops the
-- stale pin. A value the appraiser typed that happens to equal
-- the computed figure is, by definition, no override.
--
-- NOT handled: IndicatedValue <> FinalValue. That is either a real override, or a stamped figure
-- that differs from FinalValue for a mechanical reason (a partial-usage Leasehold estimate, or a
-- ProfitRent price that folded in the building). The data cannot tell those apart, and clearing
-- a real override would change the appraised value — left for a product decision.
--
-- Idempotent: a second run finds nothing to clear.
-- ============================================================

UPDATE pfv
SET pfv.[IndicatedValue] = NULL
FROM [appraisal].[PricingFinalValues] pfv
JOIN [appraisal].[PricingAnalysisMethods] pm ON pm.[Id] = pfv.[PricingMethodId]
WHERE pm.[MethodType] IN ('Leasehold', 'ProfitRent')
  AND pfv.[IndicatedValue] IS NOT NULL
  AND pfv.[IndicatedValue] = pfv.[FinalValue]
  -- Partial-usage Leasehold: FinalValue is the FULL leasehold value while the book falls back to
  -- the partial estimate (EstimatePriceRounded), so IndicatedValue = FinalValue there is a
  -- deliberate override to the full value, not a stamp. Clearing it would drop the figure.
  AND NOT EXISTS (SELECT 1 FROM [appraisal].[LeaseholdAnalyses] la
                  WHERE la.[PricingMethodId] = pm.[Id] AND la.[IsPartialUsage] = 1);
