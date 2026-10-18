-- ============================================================
-- Backfill: give every building a stored Building Cost Value.
--
-- appraisal.BuildingAppraisalDetails.BuildingCostValue (renamed from FinalCostValueOverride by
-- UseBuildingCostValueForStoredFinalCost) now always holds the value shown on the property screen: the
-- appraiser's typed figure, otherwise the one computed from the building's depreciation rows. The app
-- fills it on every save from this release on; this script does the same for buildings nobody has saved
-- since, so readers no longer have to fall back per row.
--
-- The figure is ROUND(SUM(PriceAfterDepreciation), -3) over EVERY row of
-- appraisal.BuildingDepreciationDetails (Non-Building rows included — that is what the Cost approach
-- prices) — the same rule as BuildingAppraisalDetail.ComputeBuildingCostValue and the COALESCE fallback
-- in the readers. A building with no depreciation rows stays NULL.
--
-- Sibling of 20261001090000_Backfill_BuildingInsurancePriceFromDepreciation.sql, which does the same
-- for the insurance value (IsBuilding rows only); kept separate so a database that already ran that one
-- still gets this.
--
-- Ordering: DbUp runs after every EF migration, so the renamed column already exists.
-- Idempotent: only rows whose value is still NULL are touched.
-- ============================================================

UPDATE bad
SET    bad.BuildingCostValue = x.CostValue
FROM   appraisal.BuildingAppraisalDetails bad
JOIN   (SELECT bdd.BuildingAppraisalDetailId,
               ROUND(SUM(bdd.PriceAfterDepreciation), -3) AS CostValue
        FROM   appraisal.BuildingDepreciationDetails bdd
        GROUP BY bdd.BuildingAppraisalDetailId) x
       ON x.BuildingAppraisalDetailId = bad.Id
WHERE  bad.BuildingCostValue IS NULL;

-- What moved, for the deployment log.
SELECT COUNT(*) AS Buildings,
       SUM(CASE WHEN BuildingCostValue IS NULL THEN 1 ELSE 0 END) AS StillNotEntered
FROM   appraisal.BuildingAppraisalDetails;
