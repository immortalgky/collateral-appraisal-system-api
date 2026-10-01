-- ============================================================
-- Backfill: give every building a stored insurance value.
--
-- appraisal.BuildingAppraisalDetails.BuildingInsurancePrice now always holds the value shown on the
-- property screen: the appraiser's typed figure, otherwise the one computed from the building's
-- depreciation rows. The app fills it on every save from this release on; this script does the same
-- for buildings nobody has saved since. The readers keep their per-row fallback for rows still NULL.
--
-- The figure is ROUND(SUM(PriceAfterDepreciation), -3) over the IsBuilding = 1 rows of
-- appraisal.BuildingDepreciationDetails — the same rule as BuildingAppraisalDetail.ComputeInsurancePrice
-- and BuildingInsuranceCalculator. A building with no IsBuilding rows stays NULL ("not entered").
--
-- Nothing else moves: the appraisal-level insurance total (ValuationAnalyses.InsuranceValue) already
-- used this same fallback, and is not recomputed here.
--
-- Ordering: DbUp runs after every EF migration, so the column (renamed from
-- BuildingInsurancePriceOverride by UseBuildingInsurancePriceForAppraiserValue) already exists.
-- Idempotent: only rows whose value is still NULL are touched.
-- ============================================================

UPDATE bad
SET    bad.BuildingInsurancePrice = x.InsuranceValue
FROM   appraisal.BuildingAppraisalDetails bad
JOIN   (SELECT bdd.BuildingAppraisalDetailId,
               ROUND(SUM(bdd.PriceAfterDepreciation), -3) AS InsuranceValue
        FROM   appraisal.BuildingDepreciationDetails bdd
        WHERE  bdd.IsBuilding = 1
        GROUP BY bdd.BuildingAppraisalDetailId) x
       ON x.BuildingAppraisalDetailId = bad.Id
WHERE  bad.BuildingInsurancePrice IS NULL;

-- What moved, for the deployment log.
SELECT COUNT(*) AS Buildings,
       SUM(CASE WHEN BuildingInsurancePrice IS NULL THEN 1 ELSE 0 END) AS StillNotEntered
FROM   appraisal.BuildingAppraisalDetails;
