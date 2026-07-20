-- ============================================================
-- Migrate the pricing price-unit vocabulary from the legacy MeasurementUnits
-- numeric codes to a canonical string vocabulary:
--     '01' -> 'PerSqWa'   (per Sq.Wa rate)
--     '02' -> 'PerSqm'    (per Sq.M rate)
--     '03' -> 'PerUnit'   (whole-unit lumpsum)
-- Rental period units ('04' Baht/Day, '05' Baht/Month, '06' Baht/Year) are left
-- untouched — only the pricing codes 01/02/03 are repurposed.
--
-- Also backfills the method-level PricingAnalysisMethod.UnitType so consumers can
-- read the lumpsum-vs-per-unit mode directly instead of re-deriving it.
--
-- Idempotent: every statement guards on the pre-migration values, so re-running is a no-op.
-- ============================================================

-- ------------------------------------------------------------
-- 1) Parameter group MeasurementUnits: repurpose codes 01/02/03 in place.
--    Descriptions (Baht/Sq.Wa, Baht/Sq. Meter, Baht/Unit) are intentionally kept.
-- ------------------------------------------------------------
UPDATE parameter.Parameters SET [code] = N'PerSqWa'
    WHERE [group] = N'MeasurementUnits' AND [code] = N'01';

UPDATE parameter.Parameters SET [code] = N'PerSqm'
    WHERE [group] = N'MeasurementUnits' AND [code] = N'02';

UPDATE parameter.Parameters SET [code] = N'PerUnit'
    WHERE [group] = N'MeasurementUnits' AND [code] = N'03';
GO

-- ------------------------------------------------------------
-- 2) Backfill stored per-comparable unit codes.
--    MarketComparable (source of truth) and PricingCalculation (copied at link time).
-- ------------------------------------------------------------
UPDATE appraisal.MarketComparables
    SET OfferPriceUnit = CASE OfferPriceUnit
                            WHEN N'01' THEN N'PerSqWa'
                            WHEN N'02' THEN N'PerSqm'
                            WHEN N'03' THEN N'PerUnit'
                            ELSE OfferPriceUnit END
    WHERE OfferPriceUnit IN (N'01', N'02', N'03');

UPDATE appraisal.MarketComparables
    SET SalePriceUnit = CASE SalePriceUnit
                            WHEN N'01' THEN N'PerSqWa'
                            WHEN N'02' THEN N'PerSqm'
                            WHEN N'03' THEN N'PerUnit'
                            ELSE SalePriceUnit END
    WHERE SalePriceUnit IN (N'01', N'02', N'03');

UPDATE appraisal.PricingCalculations
    SET OfferingPriceUnit = CASE OfferingPriceUnit
                            WHEN N'01' THEN N'PerSqWa'
                            WHEN N'02' THEN N'PerSqm'
                            WHEN N'03' THEN N'PerUnit'
                            ELSE OfferingPriceUnit END
    WHERE OfferingPriceUnit IN (N'01', N'02', N'03');

UPDATE appraisal.PricingCalculations
    SET SellingPriceUnit = CASE SellingPriceUnit
                            WHEN N'01' THEN N'PerSqWa'
                            WHEN N'02' THEN N'PerSqm'
                            WHEN N'03' THEN N'PerUnit'
                            ELSE SellingPriceUnit END
    WHERE SellingPriceUnit IN (N'01', N'02', N'03');
GO

-- ------------------------------------------------------------
-- 3) Backfill method-level UnitType (runs AFTER step 2 so it reads the new vocabulary).
--    Non-market methods always produce a whole-unit total -> PerUnit.
--    ValuePerUnit is intentionally left as-is; going forward the calc services populate it,
--    and the condo report already falls back to (value / area) when it is null.
-- ------------------------------------------------------------

-- 3a) Non-market methods -> PerUnit (lumpsum).
UPDATE appraisal.PricingAnalysisMethods
    SET UnitType = N'PerUnit'
    WHERE UnitType IS NULL
      AND MethodType NOT IN (N'WQS', N'SaleGrid', N'DirectComparison');
GO

-- 3b) Market methods -> dominant per-comparable unit (mirrors DetectPriceUnit: prefer the
--     offering unit when offering price is set & non-zero, else the selling unit; most frequent wins).
WITH row_units AS (
    SELECT pc.PricingMethodId,
           CASE WHEN pc.OfferingPrice IS NOT NULL AND pc.OfferingPrice <> 0
                THEN pc.OfferingPriceUnit
                ELSE pc.SellingPriceUnit END AS Unit
    FROM appraisal.PricingCalculations pc
),
unit_counts AS (
    SELECT PricingMethodId, Unit, COUNT(*) AS Cnt
    FROM row_units
    WHERE Unit IS NOT NULL AND Unit <> N''
    GROUP BY PricingMethodId, Unit
),
dominant AS (
    SELECT PricingMethodId, Unit,
           ROW_NUMBER() OVER (PARTITION BY PricingMethodId ORDER BY Cnt DESC, Unit) AS Rn
    FROM unit_counts
)
UPDATE m
    SET m.UnitType = d.Unit
    FROM appraisal.PricingAnalysisMethods m
    INNER JOIN dominant d ON d.PricingMethodId = m.Id AND d.Rn = 1
    WHERE m.UnitType IS NULL
      AND m.MethodType IN (N'WQS', N'SaleGrid', N'DirectComparison');
GO
