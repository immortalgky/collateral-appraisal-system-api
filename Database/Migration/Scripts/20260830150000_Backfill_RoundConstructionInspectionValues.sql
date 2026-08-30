-- CA-614: round every construction-inspection money figure to whole baht.
--
-- The reports for ตรวจงวดงาน must not print satang. From this release the values are rounded where
-- they are produced — Appraisal.Domain.Appraisals.ConstructionMoney for the persisted full-detail
-- figures, and ROUND(..., 0) in the three read-time aggregates that derive a summary-mode
-- inspection from its percentage. That only covers inspections saved from now on, so this script
-- brings the rows already in the database onto the same footing.
--
-- ROUND rounds halves away from zero, which is the "0.50 rounds up" rule the business asked for and
-- matches MidpointRounding.AwayFromZero in the domain.
--
-- Rounding the leaf rows rather than the report totals is deliberate: every consumer only SUMs
-- these columns, and a sum of whole baht is whole baht, so a detail table still adds up to the
-- total printed beside it.
--
-- Idempotent — the WHERE guard makes a re-run a no-op.
--
-- ConstructionInspections.SummaryPreviousValue / SummaryCurrentValue are deliberately NOT touched.
-- Those two columns are dead: the inspection screen computes the figures in a useMemo for display
-- and never writes them back into the form, so every stored row holds 0. Reads derive the value
-- from SummaryCurrentProgressPct instead — see IConstructionCurrentValueService.

UPDATE appraisal.ConstructionWorkDetails
SET ConstructionValue     = ROUND(ConstructionValue, 0),
    PreviousPropertyValue = ROUND(PreviousPropertyValue, 0),
    CurrentPropertyValue  = ROUND(CurrentPropertyValue, 0)
WHERE ConstructionValue     <> ROUND(ConstructionValue, 0)
   OR PreviousPropertyValue <> ROUND(PreviousPropertyValue, 0)
   OR CurrentPropertyValue  <> ROUND(CurrentPropertyValue, 0);

UPDATE appraisal.ConstructionInspections
SET TotalValue = ROUND(TotalValue, 0)
WHERE TotalValue <> ROUND(TotalValue, 0);
