/*==============================================================================
  BackfillEngagementCurrentValue.sql
  ------------------------------------------------------------------------------
  Purpose : Populate collateral.CollateralEngagements.CurrentValue for engagements
            that already existed before the column was added.

            CurrentValue is the appraisal value AS IT STANDS, with part-built
            buildings counted at their construction progress instead of at 100%:

                land + buildings with no inspection + inspected buildings at progress

            It is computed by the Appraisal module's IConstructionCurrentValueService
            and frozen onto the engagement at creation time. Existing engagements
            were created before that existed, so the column is NULL for all of them.

            Why it matters: the regulatory export's field 7 (Appraisal Value as
            Completed) reads this column and falls back to the full appraised value
            when it is NULL. That fallback is correct for finished collateral but
            WRONG for anything still under construction — it would report the
            as-completed value as though the building were done.

            The alternative to this script is clearing CollateralMasters and
            re-running the collateral backfill, which also rebuilds engagements.
            This script avoids destroying data and takes seconds instead.

  Run this MANUALLY (SSMS / sqlcmd). It is NOT part of DbUp/EF migrations, so it
  will not run on deploy — run it once per environment after the
  AddEngagementCurrentValue migration has been applied.

  DERIVATION — mirrors IConstructionCurrentValueService exactly, so backfilled rows
  are indistinguishable from newly-created ones:

    landValue     = SUM(PricingFinalValues.LandValue) over the appraisal's property
                    groups (SubjectType = 0 anchors on PropertyGroup)
    nonCiBuilding = SUM(BuildingDepreciationDetails.PriceAfterDepreciation) for
                    building properties with NO ConstructionInspection — no
                    inspection means the building was already finished
    ciCurrent     = per inspected property:
                      IsFullDetail = 1 -> SUM(ConstructionWorkDetails.CurrentPropertyValue)
                                          (server-computed on save)
                      IsFullDetail = 0 -> ci.TotalValue * SummaryCurrentProgressPct / 100

    CurrentValue  = landValue + nonCiBuilding + ciCurrent

  IMPORTANT — summary mode multiplies by the stored PERCENT, and deliberately does
  NOT read ci.SummaryCurrentValue. The CI screen computes that figure in a useMemo
  and displays it but never writes it back into the form, so the persisted column
  holds the default 0 while the screen showed something else. The percent is bound
  to a real input and does persist. Reading the value column would silently drop
  the whole part-built building from the regulatory file.

  SCOPE / GUARDS (all required):
    - e.CurrentValue IS NULL     -> idempotent; never overwrites a value written by
                                    the service. Safe to re-run.
    - ci.TotalValue > 0 (summed) -> matches the service returning NULL when the
                                    appraisal has no inspection worth valuing. An
                                    appraisal with no inspection at all is LEFT NULL
                                    on purpose: nothing was part-built, so field 7's
                                    fallback to the appraised value is already right.
    - a.IsDeleted = 0            -> parity with the EF path's global soft-delete filter.

  Engagements on appraisals with no construction inspection stay NULL. That is the
  expected end state, not an incomplete run — do not "fix" it by writing the
  appraised value into the column, because a non-NULL value asserts "this figure was
  computed", and the writer's fallback already covers the case.
==============================================================================*/

-- Required: CollateralEngagements carries filtered indexes, and SQL Server refuses to UPDATE a table
-- with one unless QUOTED_IDENTIFIER is ON (error 1934). SSMS defaults it ON, sqlcmd does NOT — setting
-- it here makes the script behave the same however it is run.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

/*  PREVIEW — run this on its own first to see what would change.
    Uncomment the block, inspect, then run the UPDATE below.

WITH CiAgg AS (
    SELECT      ap.AppraisalId,
                SUM(ci.TotalValue) AS InspectedTotalValue,
                SUM(CASE WHEN ci.IsFullDetail = 0
                         THEN ci.TotalValue * ISNULL(ci.SummaryCurrentProgressPct, 0) / 100.0
                         ELSE ISNULL(wd.CurrentPropertyValueSum, 0)
                    END) AS InspectedCurrentValue
    FROM        appraisal.ConstructionInspections ci
    INNER JOIN  appraisal.AppraisalProperties     ap ON ap.Id = ci.AppraisalPropertyId
    LEFT JOIN   (
                    SELECT      ConstructionInspectionId,
                                SUM(CurrentPropertyValue) AS CurrentPropertyValueSum
                    FROM        appraisal.ConstructionWorkDetails
                    GROUP BY    ConstructionInspectionId
                ) wd ON wd.ConstructionInspectionId = ci.Id
    GROUP BY    ap.AppraisalId
),
LandAgg AS (
    SELECT      pg.AppraisalId, SUM(pfv.LandValue) AS LandValue
    FROM        appraisal.PricingFinalValues        pfv
    INNER JOIN  appraisal.PricingAnalysisMethods    pam ON pam.Id = pfv.PricingMethodId
    INNER JOIN  appraisal.PricingAnalysisApproaches paa ON paa.Id = pam.ApproachId
    INNER JOIN  appraisal.PricingAnalysis           pa  ON pa.Id  = paa.PricingAnalysisId
                                                       AND pa.SubjectType = 0
    INNER JOIN  appraisal.PropertyGroups            pg  ON pg.Id  = pa.AnchorId
    GROUP BY    pg.AppraisalId
),
NonCiAgg AS (
    SELECT      ap.AppraisalId, SUM(bdd.PriceAfterDepreciation) AS CompletedBuildingValue
    FROM        appraisal.BuildingDepreciationDetails bdd
    INNER JOIN  appraisal.BuildingAppraisalDetails    bad ON bad.Id = bdd.BuildingAppraisalDetailId
    INNER JOIN  appraisal.AppraisalProperties         ap  ON ap.Id  = bad.AppraisalPropertyId
    WHERE       NOT EXISTS (
                    SELECT 1 FROM appraisal.ConstructionInspections ci
                    WHERE  ci.AppraisalPropertyId = ap.Id
                )
    GROUP BY    ap.AppraisalId
)
SELECT      e.AppraisalNumber,
            e.AppraisalValue                        AS ExistingAppraisalValue,
            ISNULL(l.LandValue, 0)                  AS LandValue,
            ISNULL(n.CompletedBuildingValue, 0)     AS CompletedBuildingValue,
            c.InspectedTotalValue,
            c.InspectedCurrentValue,
            ISNULL(l.LandValue, 0)
          + ISNULL(n.CompletedBuildingValue, 0)
          + c.InspectedCurrentValue                 AS NewCurrentValue
FROM        collateral.CollateralEngagements e
INNER JOIN  appraisal.Appraisals             a ON a.Id = e.AppraisalId AND a.IsDeleted = 0
INNER JOIN  CiAgg                            c ON c.AppraisalId = e.AppraisalId
LEFT JOIN   LandAgg                          l ON l.AppraisalId = e.AppraisalId
LEFT JOIN   NonCiAgg                         n ON n.AppraisalId = e.AppraisalId
WHERE       e.CurrentValue IS NULL
  AND       c.InspectedTotalValue > 0
ORDER BY    e.AppraisalNumber;
*/

BEGIN TRANSACTION;

WITH CiAgg AS (
    SELECT      ap.AppraisalId,
                SUM(ci.TotalValue) AS InspectedTotalValue,
                SUM(CASE WHEN ci.IsFullDetail = 0
                         THEN ci.TotalValue * ISNULL(ci.SummaryCurrentProgressPct, 0) / 100.0
                         ELSE ISNULL(wd.CurrentPropertyValueSum, 0)
                    END) AS InspectedCurrentValue
    FROM        appraisal.ConstructionInspections ci
    INNER JOIN  appraisal.AppraisalProperties     ap ON ap.Id = ci.AppraisalPropertyId
    LEFT JOIN   (
                    SELECT      ConstructionInspectionId,
                                SUM(CurrentPropertyValue) AS CurrentPropertyValueSum
                    FROM        appraisal.ConstructionWorkDetails
                    GROUP BY    ConstructionInspectionId
                ) wd ON wd.ConstructionInspectionId = ci.Id
    GROUP BY    ap.AppraisalId
),
LandAgg AS (
    SELECT      pg.AppraisalId, SUM(pfv.LandValue) AS LandValue
    FROM        appraisal.PricingFinalValues        pfv
    INNER JOIN  appraisal.PricingAnalysisMethods    pam ON pam.Id = pfv.PricingMethodId
    INNER JOIN  appraisal.PricingAnalysisApproaches paa ON paa.Id = pam.ApproachId
    INNER JOIN  appraisal.PricingAnalysis           pa  ON pa.Id  = paa.PricingAnalysisId
                                                       AND pa.SubjectType = 0
    INNER JOIN  appraisal.PropertyGroups            pg  ON pg.Id  = pa.AnchorId
    GROUP BY    pg.AppraisalId
),
NonCiAgg AS (
    SELECT      ap.AppraisalId, SUM(bdd.PriceAfterDepreciation) AS CompletedBuildingValue
    FROM        appraisal.BuildingDepreciationDetails bdd
    INNER JOIN  appraisal.BuildingAppraisalDetails    bad ON bad.Id = bdd.BuildingAppraisalDetailId
    INNER JOIN  appraisal.AppraisalProperties         ap  ON ap.Id  = bad.AppraisalPropertyId
    WHERE       NOT EXISTS (
                    SELECT 1 FROM appraisal.ConstructionInspections ci
                    WHERE  ci.AppraisalPropertyId = ap.Id
                )
    GROUP BY    ap.AppraisalId
)
UPDATE      e
SET         e.CurrentValue = ISNULL(l.LandValue, 0)
                           + ISNULL(n.CompletedBuildingValue, 0)
                           + c.InspectedCurrentValue
FROM        collateral.CollateralEngagements e
INNER JOIN  appraisal.Appraisals             a ON a.Id = e.AppraisalId AND a.IsDeleted = 0
INNER JOIN  CiAgg                            c ON c.AppraisalId = e.AppraisalId
LEFT JOIN   LandAgg                          l ON l.AppraisalId = e.AppraisalId
LEFT JOIN   NonCiAgg                         n ON n.AppraisalId = e.AppraisalId
WHERE       e.CurrentValue IS NULL
  AND       c.InspectedTotalValue > 0;

PRINT CONCAT('Backfilled CurrentValue on ', @@ROWCOUNT, ' collateral engagement row(s).');

-- Engagements deliberately left NULL: no construction inspection, so nothing is part-built and the
-- regulatory writer's fallback to the appraised value is already correct.
DECLARE @LeftNull int = (
    SELECT COUNT(*)
    FROM        collateral.CollateralEngagements e
    INNER JOIN  appraisal.Appraisals a ON a.Id = e.AppraisalId AND a.IsDeleted = 0
    WHERE       e.CurrentValue IS NULL
);
PRINT CONCAT(@LeftNull, ' engagement(s) left NULL (no construction inspection) — expected, not an error.');

COMMIT TRANSACTION;
