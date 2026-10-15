-- ============================================================
-- Data fix: recover Leasehold overrides that were about to be stranded.
--
-- LeaseholdAnalyses.EstimatePriceRounded used to hold the system-computed partial-usage estimate
-- AND the appraiser's typed-over figure interchangeably (SaveLeaseholdAnalysisCommandHandler wrote
-- `command.EstimatePriceRounded ?? estimatePriceRounded` into the one column). The handler now
-- stores the computed value there only, and moves the override into the shared
-- PricingFinalValues.IndicatedValue column (same as every other pricing method). Existing rows
-- with a real override sitting in EstimatePriceRounded would otherwise be silently read back as
-- "just the computed value" and lose the override on the next load/save cycle.
--
-- Scope of this script — the UNAMBIGUOUS case only:
--   IsPartialUsage = 0  ->  EstimatePriceRounded was NEVER touched by the partial-usage
--   calculation in the old handler (that branch only ran when IsPartialUsage = 1), so any
--   non-null value in this column on a non-partial row is necessarily something the appraiser
--   typed, never a system default. Safe to move as-is.
--
-- NOT handled here — IsPartialUsage = 1 rows are read-then-decide:
--   For those rows the old column mixed the appraiser's figure with the computed partial estimate,
--   and the computed estimate itself was built from the wrong rate (LandValuePerSqWa instead of
--   PricePerSqWa — see SaveLeaseholdAnalysisCommandHandler fix). Recomputing today's correct
--   estimate and comparing against the stored value cannot distinguish "appraiser typed this
--   exact number" from "old buggy computation landed on this number", because the rate bug alone
--   would make nearly every stored value differ from a fresh recompute regardless of whether an
--   override was ever entered. There is no reliable signal left in the data to separate the two
--   cases, so this script deliberately does not touch IsPartialUsage = 1 rows. Any real override
--   an appraiser typed on a partial-usage Leasehold method before this fix shipped is left in
--   EstimatePriceRounded, unread by the new code, and will appear to reset to the computed value
--   on that method's next load. Flagged to the team lead for a product decision rather than guessed
--   at here.
--
-- Ordering: DbUp runs after every EF migration, so PricingFinalValues.IndicatedValue already exists.
-- Idempotent: only copies where IndicatedValue is still NULL (won't overwrite a value already
-- migrated, or one set for real since); only clears EstimatePriceRounded on rows just migrated.
-- ============================================================

UPDATE pfv
SET pfv.[IndicatedValue] = la.[EstimatePriceRounded]
FROM [appraisal].[PricingFinalValues] pfv
JOIN [appraisal].[LeaseholdAnalyses] la ON la.[PricingMethodId] = pfv.[PricingMethodId]
WHERE la.[IsPartialUsage] = 0
  AND la.[EstimatePriceRounded] IS NOT NULL
  AND pfv.[IndicatedValue] IS NULL;

UPDATE la
SET la.[EstimatePriceRounded] = NULL
FROM [appraisal].[LeaseholdAnalyses] la
JOIN [appraisal].[PricingFinalValues] pfv ON pfv.[PricingMethodId] = la.[PricingMethodId]
WHERE la.[IsPartialUsage] = 0
  AND la.[EstimatePriceRounded] IS NOT NULL
  AND pfv.[IndicatedValue] = la.[EstimatePriceRounded];
