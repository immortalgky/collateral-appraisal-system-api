#!/usr/bin/env bash
# Dumps every pricing row behind one appraisal, in the order a reviewer reads them:
# group composition → approaches → methods → final values → comparable arithmetic.
#
# Written for the 2026-09-23/24 pricing fixes (Role gate on LandValue, satang rounding on the
# SAG/DC intermediates, thousand rounding, WQS price unit) so each test case can be shown as data
# rather than described. Read-only.
#
#   ./dump-pricing.sh 69000150
#   ./dump-pricing.sh 69000150 > case-land-and-building.txt
set -euo pipefail

APPRAISAL_NUMBER="${1:?usage: dump-pricing.sh <AppraisalNumber>}"

q() {
  docker exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P 'P@ssw0rd' -C -d CollateralAppraisal \
    -h -1 -W -s"|" -Q "SET NOCOUNT ON; $1" 2>&1
}

hdr() { printf '\n=== %s ===\n' "$1"; }

echo "APPRAISAL $APPRAISAL_NUMBER   (dumped $(date '+%F %T'))"

hdr "1. PROPERTY GROUPS — what is being priced"
q "
SELECT pg.GroupNumber, pg.GroupName,
       SUM(CASE WHEN l.Id IS NOT NULL THEN 1 ELSE 0 END) AS Land,
       SUM(CASE WHEN b.Id IS NOT NULL THEN 1 ELSE 0 END) AS Building,
       SUM(CASE WHEN cd.Id IS NOT NULL THEN 1 ELSE 0 END) AS Condo,
       ISNULL(SUM(l.DeductedAreaInSqWa), 0) AS DeductedSqWa
FROM appraisal.Appraisals a
JOIN appraisal.PropertyGroups pg ON pg.AppraisalId = a.Id
LEFT JOIN appraisal.PropertyGroupItems gi ON gi.PropertyGroupId = pg.Id
LEFT JOIN appraisal.LandAppraisalDetails     l  ON l.AppraisalPropertyId  = gi.AppraisalPropertyId
LEFT JOIN appraisal.BuildingAppraisalDetails b  ON b.AppraisalPropertyId  = gi.AppraisalPropertyId
LEFT JOIN appraisal.CondoAppraisalDetails    cd ON cd.AppraisalPropertyId = gi.AppraisalPropertyId
WHERE a.AppraisalNumber = '$APPRAISAL_NUMBER'
GROUP BY pg.GroupNumber, pg.GroupName ORDER BY pg.GroupNumber;"

hdr "2. APPROACHES — ApproachType | IsSelected | SelectedValue"
q "
SELECT pg.GroupNumber, paa.ApproachType, paa.IsSelected, paa.ApproachValue
FROM appraisal.Appraisals a
JOIN appraisal.PropertyGroups pg ON pg.AppraisalId = a.Id
JOIN appraisal.PricingAnalysis pan ON pan.AnchorId = pg.Id
JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pan.Id
WHERE a.AppraisalNumber = '$APPRAISAL_NUMBER'
ORDER BY pg.GroupNumber, paa.ApproachType;"

hdr "3. METHODS — Role is the gate that decides whether LandValue is written at all"
q "
SELECT pg.GroupNumber, paa.ApproachType, pm.MethodType,
       ISNULL(pm.Role,'(null)') AS Role, ISNULL(pm.UnitType,'(null)') AS UnitType,
       pm.MethodValue, pm.ValuePerUnit, pm.IsSelected, pm.UseSystemCalc
FROM appraisal.Appraisals a
JOIN appraisal.PropertyGroups pg ON pg.AppraisalId = a.Id
JOIN appraisal.PricingAnalysis pan ON pan.AnchorId = pg.Id
JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pan.Id
JOIN appraisal.PricingAnalysisMethods pm ON pm.ApproachId = paa.Id
WHERE a.AppraisalNumber = '$APPRAISAL_NUMBER'
ORDER BY pg.GroupNumber, paa.ApproachType, pm.MethodType;"

hdr "4. FINAL VALUES — LandArea/LandValue must be NULL for every Role=(null) method"
q "
SELECT pg.GroupNumber, paa.ApproachType, pm.MethodType, ISNULL(pm.Role,'(null)') AS Role,
       fv.FinalValue, fv.FinalValueOverride, fv.IndicatedValue,
       ISNULL(fv.FinalValueUnitType,'(null)') AS FvUnitType,
       fv.IncludeLandArea, fv.LandArea, fv.LandValue,
       fv.HasBuildingValue, fv.BuildingValue
FROM appraisal.Appraisals a
JOIN appraisal.PropertyGroups pg ON pg.AppraisalId = a.Id
JOIN appraisal.PricingAnalysis pan ON pan.AnchorId = pg.Id
JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pan.Id
JOIN appraisal.PricingAnalysisMethods pm ON pm.ApproachId = paa.Id
JOIN appraisal.PricingFinalValues fv ON fv.PricingMethodId = pm.Id
WHERE a.AppraisalNumber = '$APPRAISAL_NUMBER'
ORDER BY pg.GroupNumber, paa.ApproachType, pm.MethodType;"

hdr "5. COMPARABLES — the satang-rounded intermediates, and the unit WQS used to drop"
q "
SELECT paa.ApproachType, pm.MethodType,
       c.OfferingPrice, ISNULL(c.OfferingPriceUnit,'(null)') AS OfferUnit,
       c.SellingPrice,  ISNULL(c.SellingPriceUnit,'(null)')  AS SellUnit,
       c.LandValueAdjustment, c.BuildingValueAdjustment,
       c.TotalFactorDiffAmt, c.TotalAdjustedValue, c.Weight, c.WeightedAdjustedValue
FROM appraisal.Appraisals a
JOIN appraisal.PropertyGroups pg ON pg.AppraisalId = a.Id
JOIN appraisal.PricingAnalysis pan ON pan.AnchorId = pg.Id
JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pan.Id
JOIN appraisal.PricingAnalysisMethods pm ON pm.ApproachId = paa.Id
JOIN appraisal.PricingCalculations c ON c.PricingMethodId = pm.Id
WHERE a.AppraisalNumber = '$APPRAISAL_NUMBER'
ORDER BY paa.ApproachType, pm.MethodType, c.SellingPrice;"

hdr "6. RULE CHECKS — every row must read OK"
q "
WITH m AS (
  SELECT pm.Id, pm.MethodType, pm.Role, pm.UnitType, pm.ValuePerUnit, paa.ApproachType,
         fv.LandValue, fv.LandArea, fv.FinalValue, fv.IndicatedValue
  FROM appraisal.Appraisals a
  JOIN appraisal.PropertyGroups pg ON pg.AppraisalId = a.Id
  JOIN appraisal.PricingAnalysis pan ON pan.AnchorId = pg.Id
  JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pan.Id
  JOIN appraisal.PricingAnalysisMethods pm ON pm.ApproachId = paa.Id
  LEFT JOIN appraisal.PricingFinalValues fv ON fv.PricingMethodId = pm.Id
  WHERE a.AppraisalNumber = '$APPRAISAL_NUMBER'
)
SELECT ApproachType, MethodType,
       CASE WHEN Role IS NULL AND (LandValue IS NOT NULL OR LandArea IS NOT NULL)
            THEN 'FAIL: non-cost method wrote land figures' ELSE 'OK' END AS RoleGate,
       CASE WHEN UnitType IN ('PerSqWa','PerSqm') AND ValuePerUnit IS NULL
            THEN 'FAIL: rate unit but no ValuePerUnit' ELSE 'OK' END AS RateHasValuePerUnit,
       CASE WHEN UnitType = 'PerUnit' AND ValuePerUnit IS NOT NULL
            THEN 'FAIL: lumpsum carries a rate' ELSE 'OK' END AS LumpsumHasNoRate
FROM m ORDER BY ApproachType, MethodType;"
