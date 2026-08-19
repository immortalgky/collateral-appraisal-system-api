/*==============================================================================
  BackfillEngagementConstructionProgress.sql
  ------------------------------------------------------------------------------
  Purpose : Populate collateral.CollateralEngagements.IsUnderConstruction and
            .ConstructionProgressPercent for engagements created before those
            columns existed.

            Both figures used to live on collateral.LandDetails as
            IsUnderConstructionAtLastAppraisal / OverallConstructionProgressPercent.
            That was wrong on two counts:

              1. LandDetails is a latest-wins cache on a mutable row. Re-processing
                 an older appraisal after a newer one overwrote the newer state with
                 the older one, with no error and no signal.
              2. The value came from ONE property's inspection
                 (primaryProperty.ConstructionInspection.OverallCurrentProgressPercent),
                 so an appraisal covering several buildings reported whatever the
                 primary property happened to say and ignored the rest.

            The engagement columns fix both: frozen per appraisal, and weighted by
            value across EVERY inspected building.

  Run this MANUALLY (SSMS / sqlcmd). It is NOT part of DbUp/EF migrations, so it
  will not run on deploy — run it once per environment after the
  AddEngagementConstructionStatus migration has been applied.

  DERIVATION — mirrors ConstructionValueBreakdown exactly, so backfilled rows are
  indistinguishable from newly-created ones:

    InspectedTotalValue   = SUM(ConstructionInspections.TotalValue)
    InspectedCurrentValue = per inspected property:
                              IsFullDetail = 1 -> SUM(ConstructionWorkDetails.CurrentPropertyValue)
                                                  (server-computed on save)
                              IsFullDetail = 0 -> ci.TotalValue * SummaryCurrentProgressPct / 100

    IsUnderConstruction         = InspectedTotalValue > 0
                                  AND InspectedCurrentValue < InspectedTotalValue
    ConstructionProgressPercent = clamp(InspectedCurrentValue / InspectedTotalValue * 100, 0, 100)

  IMPORTANT — summary mode multiplies by the stored PERCENT, and deliberately does
  NOT read ci.SummaryCurrentValue. The CI screen computes that figure in a useMemo
  and displays it but never writes it back into the form, so the persisted column
  holds the default 0 while the screen showed something else. The percent is bound
  to a real input and does persist. This matches BackfillEngagementCurrentValue.sql
  and IConstructionCurrentValueService — all three must stay in step.

  SCOPE / GUARDS (all required):
    - e.IsUnderConstruction IS NULL -> idempotent; never overwrites a value written
                                       by the service. Safe to re-run.
    - ci.TotalValue > 0 (summed)    -> matches the service returning NULL when the
                                       appraisal has no inspection worth valuing.
    - a.IsDeleted = 0               -> parity with the EF path's soft-delete filter.

  Engagements on appraisals with no construction inspection stay NULL. That is the
  expected end state: nothing was part-built, so the regulatory export's "completed"
  branch (progress 100, under-construction blank) is already correct for them.
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
)
SELECT      e.AppraisalNumber,
            c.InspectedTotalValue,
            c.InspectedCurrentValue,
            CASE WHEN c.InspectedCurrentValue < c.InspectedTotalValue THEN 1 ELSE 0 END AS NewIsUnderConstruction,
            CAST(CASE
                     WHEN c.InspectedCurrentValue <= 0                     THEN 0
                     WHEN c.InspectedCurrentValue >= c.InspectedTotalValue THEN 100
                     ELSE c.InspectedCurrentValue / c.InspectedTotalValue * 100.0
                 END AS decimal(7,4))                                       AS NewProgressPercent,
            ld.IsUnderConstructionAtLastAppraisal                           AS OldFlagOnLandDetail,
            ld.OverallConstructionProgressPercent                          AS OldPercentOnLandDetail
FROM        collateral.CollateralEngagements e
INNER JOIN  appraisal.Appraisals             a  ON a.Id  = e.AppraisalId AND a.IsDeleted = 0
INNER JOIN  CiAgg                            c  ON c.AppraisalId = e.AppraisalId
LEFT JOIN   collateral.LandDetails           ld ON ld.CollateralMasterId = e.CollateralMasterId
WHERE       e.IsUnderConstruction IS NULL
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
)
UPDATE      e
SET         e.IsUnderConstruction =
                CASE WHEN c.InspectedCurrentValue < c.InspectedTotalValue THEN 1 ELSE 0 END,
            -- Clamped to 0–100 the same way ConstructionValueBreakdown does: a summary percent is a
            -- free-typed input with no validator, so a stray 150 must not reach the regulatory file.
            e.ConstructionProgressPercent =
                CAST(CASE
                         WHEN c.InspectedCurrentValue <= 0                     THEN 0
                         WHEN c.InspectedCurrentValue >= c.InspectedTotalValue THEN 100
                         ELSE c.InspectedCurrentValue / c.InspectedTotalValue * 100.0
                     END AS decimal(7,4))
FROM        collateral.CollateralEngagements e
INNER JOIN  appraisal.Appraisals             a ON a.Id = e.AppraisalId AND a.IsDeleted = 0
INNER JOIN  CiAgg                            c ON c.AppraisalId = e.AppraisalId
WHERE       e.IsUnderConstruction IS NULL
  AND       c.InspectedTotalValue > 0;

PRINT CONCAT('Backfilled construction status on ', @@ROWCOUNT, ' collateral engagement row(s).');

-- Engagements deliberately left NULL: no construction inspection, so nothing is part-built and the
-- regulatory export's "completed" branch already reports progress 100 with a blank flag.
DECLARE @LeftNull int = (
    SELECT COUNT(*)
    FROM        collateral.CollateralEngagements e
    INNER JOIN  appraisal.Appraisals a ON a.Id = e.AppraisalId AND a.IsDeleted = 0
    WHERE       e.IsUnderConstruction IS NULL
);
PRINT CONCAT(@LeftNull, ' engagement(s) left NULL (no construction inspection) — expected, not an error.');

COMMIT TRANSACTION;
