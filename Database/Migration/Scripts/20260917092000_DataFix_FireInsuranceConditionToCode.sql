-- ============================================================
-- Data fix: FireInsuranceCode columns now hold the rate CODE ('01'..'12')
--           instead of the condition string ('GreaterThan8Floors', ...).
--
-- Why: the condition string was the join key into the rate table, so renaming a
-- condition would silently orphan every appraisal that referenced it — the lookup
-- returns null and BuildingInsurancePrice / CoverageAmount quietly become null with
-- no error. Code is the rate table's primary key and is stable.
--
-- Crosswalk: appraisal.FireInsuranceRates (Code <-> Condition).
-- Ships WITH the code change that switches every lookup to Code — the two must go
-- out together or new saves stop resolving a rate.
--
-- Three columns carry the value:
--   appraisal.CondoAppraisalDetails.FireInsuranceCode
--   appraisal.ProjectModels.FireInsuranceCode
--   appraisal.ProjectModelAssumptions.FireInsuranceCode
--
-- Safe to re-run: the JOIN only matches rows still holding a condition string, and a
-- code never equals a condition, so a second pass converts nothing.
-- Rows holding '' or NULL are left untouched — the code treats both as "not chosen".
-- ============================================================

UPDATE cad
SET    cad.FireInsuranceCode = r.Code
FROM   appraisal.CondoAppraisalDetails cad
JOIN   appraisal.FireInsuranceRates r
       ON r.Condition = cad.FireInsuranceCode
WHERE  cad.FireInsuranceCode IS NOT NULL
  AND  cad.FireInsuranceCode <> '';

UPDATE pm
SET    pm.FireInsuranceCode = r.Code
FROM   appraisal.ProjectModels pm
JOIN   appraisal.FireInsuranceRates r
       ON r.Condition = pm.FireInsuranceCode
WHERE  pm.FireInsuranceCode IS NOT NULL
  AND  pm.FireInsuranceCode <> '';

UPDATE pma
SET    pma.FireInsuranceCode = r.Code
FROM   appraisal.ProjectModelAssumptions pma
JOIN   appraisal.FireInsuranceRates r
       ON r.Condition = pma.FireInsuranceCode
WHERE  pma.FireInsuranceCode IS NOT NULL
  AND  pma.FireInsuranceCode <> '';

-- Report anything left behind: a non-empty value that is neither a known code nor a
-- known condition means someone stored a free string (the block/project write paths
-- had no validator until this change), and its rate will not resolve.
SELECT 'CondoAppraisalDetails' AS TableName, cad.FireInsuranceCode AS UnmatchedValue, COUNT(*) AS Rows
FROM   appraisal.CondoAppraisalDetails cad
WHERE  cad.FireInsuranceCode IS NOT NULL AND cad.FireInsuranceCode <> ''
  AND  NOT EXISTS (SELECT 1 FROM appraisal.FireInsuranceRates r WHERE r.Code = cad.FireInsuranceCode)
GROUP BY cad.FireInsuranceCode
UNION ALL
SELECT 'ProjectModels', pm.FireInsuranceCode, COUNT(*)
FROM   appraisal.ProjectModels pm
WHERE  pm.FireInsuranceCode IS NOT NULL AND pm.FireInsuranceCode <> ''
  AND  NOT EXISTS (SELECT 1 FROM appraisal.FireInsuranceRates r WHERE r.Code = pm.FireInsuranceCode)
GROUP BY pm.FireInsuranceCode
UNION ALL
SELECT 'ProjectModelAssumptions', pma.FireInsuranceCode, COUNT(*)
FROM   appraisal.ProjectModelAssumptions pma
WHERE  pma.FireInsuranceCode IS NOT NULL AND pma.FireInsuranceCode <> ''
  AND  NOT EXISTS (SELECT 1 FROM appraisal.FireInsuranceRates r WHERE r.Code = pma.FireInsuranceCode)
GROUP BY pma.FireInsuranceCode;
