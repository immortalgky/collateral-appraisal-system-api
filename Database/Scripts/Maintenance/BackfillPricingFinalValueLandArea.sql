/*==============================================================================
  BackfillPricingFinalValueLandArea.sql
  ------------------------------------------------------------------------------
  Purpose : Populate appraisal.PricingFinalValues.LandArea / LandValue for
            historical rows priced at a per-unit RATE (PerSqWa / PerSqm), where
            they were never written.

            Root cause: all three save handlers (SaveComparativeAnalysis,
            SetFinalValue, UpdateFinalValue) gated the write on
                IncludeLandArea == true AND LandArea.HasValue AND LandValue.HasValue
            while the WQS screen only ever sent LandValue when the *building-cost*
            toggle was on (mapWQSFormToSubmitSchema.ts:23). On a market/no-building
            group LandValue arrived NULL, the conjunction failed, and — because the
            else-branch only caught IncludeLandArea == false — nothing was written
            and no error was raised.

            This matters because two consumers aggregate the column behind
            ISNULL(..., 0) and therefore silently reported 0:
              - GetDecisionSummaryQueryHandler.cs      (SUM(pfv.LandValue))
              - AppraisalSummaryConstructionDataProvider.cs (CI summary land value)

            The forward fix derives both server-side whenever the method's unit is
            a per-unit rate, so rows saved after that deploy self-populate and are
            not touched here. Run this AFTER that fix ships.

  Run this MANUALLY (SSMS / sqlcmd). It is NOT part of DbUp/EF migrations.

  DERIVATION — mirrors the forward fix exactly, so backfilled rows are
  indistinguishable from newly-saved ones:
    LandArea  = SUM over the group's land titles of
                  (AreaRai * 400) + (AreaNgan * 100) + AreaSquareWa
                which is precisely LandArea.TotalSquareWa in the domain
                (LandArea.cs:30) summed by LandAppraisalDetail.TotalLandAreaInSqWa
                (LandAppraisalDetail.cs:120) — the same value the forward fix reads
                via PricingPropertyDataService.GetTotalLandAreaFromTitlesAsync.
    Rate      = COALESCE(PricingAnalysisMethods.ValuePerUnit,
                         PricingFinalValues.FinalValueAdjusted)
    LandValue = LandArea * Rate

  SCOPE / GUARDS (all required):
    - fv.LandArea IS NULL          -> idempotent; never overwrites a hand-entered
                                      value (cost approach enters land value by hand).
    - m.UnitType IN ('PerSqWa','PerSqm')
                                   -> a PerUnit method is a whole-unit lumpsum and
                                      carries no land rate; deriving one would be
                                      fabricating data.
    - fv.IncludeLandArea = 1       -> respects a deliberate ExcludeLandArea().
    - fv.HasBuildingValue is not 1 -> IMPORTANT. For a rate method the derived
                                      LandValue equals the group's WHOLE appraisal
                                      price (e.g. 4,241 sq.wa x 57,000 = 241,737,000)
                                      even when the group contains a building. Where
                                      a separate BuildingValue is already recorded,
                                      writing that same total as LandValue would
                                      double-count in both SUM(pfv.LandValue)
                                      consumers above. Those rows are skipped.
    - pa.SubjectType = 0           -> PropertyGroup only. Reference subject types
                                      (MachineryCostRef..ProfitRentRef) anchor to a
                                      non-group field, so AnchorId is NOT a group id.
    - a.IsDeleted = 0              -> parity with the EF path, which applies a global
                                      soft-delete query filter on the Appraisal root.
    - computed area > 0 and rate IS NOT NULL
                                   -> groups with no land titles are skipped.

  IDEMPOTENT: re-running affects 0 rows.
==============================================================================*/

SET NOCOUNT ON;
-- Ensure any runtime error aborts and rolls back rather than leaving the transaction
-- open (holding locks on live appraisal.PricingFinalValues) for an operator to clean up.
SET XACT_ABORT ON;

-- ─────────────────────────────────────────────────────────────────────────────
-- PREVIEW — uncomment and run this SELECT first to eyeball the affected rows.
-- Sanity-check a few: TotalSquareWa * Rate should reconcile with fv.AppraisalPrice.
-- ─────────────────────────────────────────────────────────────────────────────
/*
SELECT  fv.Id                                   AS PricingFinalValueId,
        pg.GroupNumber,
        m.UnitType,
        src.TotalSquareWa                       AS NewLandArea,
        COALESCE(m.ValuePerUnit, fv.FinalValueAdjusted) AS Rate,
        src.TotalSquareWa
          * COALESCE(m.ValuePerUnit, fv.FinalValueAdjusted)          AS NewLandValue,
        fv.AppraisalPrice                       AS ExistingAppraisalPrice,
        fv.LandArea                             AS ExistingLandArea,
        fv.LandValue                            AS ExistingLandValue
FROM        appraisal.PricingFinalValues        fv
INNER JOIN  appraisal.PricingAnalysisMethods    m   ON m.Id  = fv.PricingMethodId
INNER JOIN  appraisal.PricingAnalysisApproaches apr ON apr.Id = m.ApproachId
INNER JOIN  appraisal.PricingAnalysis           pa  ON pa.Id  = apr.PricingAnalysisId
                                                   AND pa.SubjectType = 0
INNER JOIN  appraisal.PropertyGroups            pg  ON pg.Id  = pa.AnchorId
INNER JOIN  appraisal.Appraisals                a   ON a.Id   = pg.AppraisalId
                                                   AND a.IsDeleted = 0
CROSS APPLY (
    SELECT TotalSquareWa = SUM( (ISNULL(lt.AreaRai, 0)  * 400)
                              + (ISNULL(lt.AreaNgan, 0) * 100)
                              +  ISNULL(lt.AreaSquareWa, 0) )
    FROM       appraisal.PropertyGroupItems    gi
    INNER JOIN appraisal.AppraisalProperties   p   ON p.Id   = gi.AppraisalPropertyId
    INNER JOIN appraisal.LandAppraisalDetails  lad ON lad.AppraisalPropertyId = p.Id
    INNER JOIN appraisal.LandTitles            lt  ON lt.LandAppraisalDetailId = lad.Id
    WHERE gi.PropertyGroupId = pg.Id
) src
WHERE   fv.LandArea IS NULL
  AND   m.UnitType IN ('PerSqWa', 'PerSqm')
  AND   fv.IncludeLandArea = 1
  AND   ISNULL(fv.HasBuildingValue, 0) = 0
  AND   src.TotalSquareWa > 0
  AND   COALESCE(m.ValuePerUnit, fv.FinalValueAdjusted) IS NOT NULL
ORDER BY pg.GroupNumber;
*/

BEGIN TRANSACTION;

UPDATE  fv
SET     fv.LandArea  = src.TotalSquareWa,
        fv.LandValue = src.TotalSquareWa
                     * COALESCE(m.ValuePerUnit, fv.FinalValueAdjusted)
FROM        appraisal.PricingFinalValues        fv
INNER JOIN  appraisal.PricingAnalysisMethods    m   ON m.Id  = fv.PricingMethodId
INNER JOIN  appraisal.PricingAnalysisApproaches apr ON apr.Id = m.ApproachId
INNER JOIN  appraisal.PricingAnalysis           pa  ON pa.Id  = apr.PricingAnalysisId
                                                   AND pa.SubjectType = 0
INNER JOIN  appraisal.PropertyGroups            pg  ON pg.Id  = pa.AnchorId
INNER JOIN  appraisal.Appraisals                a   ON a.Id   = pg.AppraisalId
                                                   AND a.IsDeleted = 0
CROSS APPLY (
    SELECT TotalSquareWa = SUM( (ISNULL(lt.AreaRai, 0)  * 400)
                              + (ISNULL(lt.AreaNgan, 0) * 100)
                              +  ISNULL(lt.AreaSquareWa, 0) )
    FROM       appraisal.PropertyGroupItems    gi
    INNER JOIN appraisal.AppraisalProperties   p   ON p.Id   = gi.AppraisalPropertyId
    INNER JOIN appraisal.LandAppraisalDetails  lad ON lad.AppraisalPropertyId = p.Id
    INNER JOIN appraisal.LandTitles            lt  ON lt.LandAppraisalDetailId = lad.Id
    WHERE gi.PropertyGroupId = pg.Id
) src
WHERE   fv.LandArea IS NULL
  AND   m.UnitType IN ('PerSqWa', 'PerSqm')
  AND   fv.IncludeLandArea = 1
  AND   ISNULL(fv.HasBuildingValue, 0) = 0
  AND   src.TotalSquareWa > 0
  AND   COALESCE(m.ValuePerUnit, fv.FinalValueAdjusted) IS NOT NULL;

PRINT CONCAT('Backfilled LandArea/LandValue on ', @@ROWCOUNT, ' pricing final value row(s).');

COMMIT TRANSACTION;
