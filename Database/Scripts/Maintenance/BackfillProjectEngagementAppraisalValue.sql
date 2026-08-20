/*==============================================================================
  BackfillProjectEngagementAppraisalValue.sql
  ------------------------------------------------------------------------------
  Purpose : Correct collateral.CollateralEngagements.AppraisalValue for block-project
            (PRJ) engagements created before the source was fixed.

            The upsert used to pass ProjectDetails.ProjectSellingPrice — the
            developer's LIST price — into the engagement's AppraisalValue. That
            column means the same thing for every other collateral type: what the
            appraiser valued it at. The correct figure is the sum of the per-unit
            appraised values (appraisal.ProjectUnitPrices.TotalAppraisalValueRounded,
            carried to collateral.ProjectUnits.LastAppraisedValue).

            The two are unrelated and diverge in both directions. Observed on dev:

              project A : list 29,650,000  vs appraised  3,000,000
              project B : list 24,400,000  vs appraised 55,154,250

  Run this MANUALLY (SSMS / sqlcmd). It is NOT part of DbUp/EF migrations, so it
  will not run on deploy — run it once per environment after deploying the upsert
  change that switched the source.

  SCOPE / GUARDS (all required):
    - e.AppraisedCollateralType = 'PRJ'  -> only block projects were ever mis-sourced.
    - e.AppraisalValue = pd.ProjectSellingPrice
                                         -> only rows that still carry the list price.
                                            A row already holding the appraised total
                                            (written by the fixed code) is left alone,
                                            which makes this safe to re-run. The rare
                                            case where the two figures happen to be
                                            equal is a no-op either way.
    - SUM(pu.LastAppraisedValue) IS NOT NULL
                                         -> a project with no unit priced yet has no
                                            appraised total to write; leaving the row
                                            untouched beats overwriting it with 0.
    - a.IsDeleted = 0                    -> parity with the EF path's soft-delete filter.

  NOTE: ProjectDetails.ProjectSellingPrice is NOT removed. Once the engagement stops
  borrowing it, it carries exactly one meaning — the project's list price — and no
  longer duplicates anything.
==============================================================================*/

-- Required: CollateralEngagements carries filtered indexes, and SQL Server refuses to UPDATE a table
-- with one unless QUOTED_IDENTIFIER is ON (error 1934). SSMS defaults it ON, sqlcmd does NOT.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

/*  PREVIEW — run this on its own first to see what would change.

SELECT      e.AppraisalNumber,
            e.AppraisalValue          AS CurrentValue_ListPrice,
            pd.ProjectSellingPrice,
            ua.AppraisedTotal         AS NewValue_Appraised,
            pd.TotalUnits,
            ua.PricedUnits
FROM        collateral.CollateralEngagements e
INNER JOIN  appraisal.Appraisals    a  ON a.Id  = e.AppraisalId AND a.IsDeleted = 0
INNER JOIN  collateral.ProjectDetails pd ON pd.CollateralMasterId = e.CollateralMasterId
CROSS APPLY (
                SELECT  SUM(pu.LastAppraisedValue)                              AS AppraisedTotal,
                        COUNT(CASE WHEN pu.LastAppraisedValue IS NOT NULL THEN 1 END) AS PricedUnits
                FROM    collateral.ProjectUnits pu
                WHERE   pu.CollateralMasterId = e.CollateralMasterId
            ) ua
WHERE       e.AppraisedCollateralType = 'PRJ'
  AND       ua.AppraisedTotal IS NOT NULL
  AND       e.AppraisalValue = pd.ProjectSellingPrice
ORDER BY    e.AppraisalNumber;
*/

BEGIN TRANSACTION;

UPDATE      e
SET         e.AppraisalValue = ua.AppraisedTotal
FROM        collateral.CollateralEngagements e
INNER JOIN  appraisal.Appraisals      a  ON a.Id = e.AppraisalId AND a.IsDeleted = 0
INNER JOIN  collateral.ProjectDetails pd ON pd.CollateralMasterId = e.CollateralMasterId
CROSS APPLY (
                SELECT  SUM(pu.LastAppraisedValue) AS AppraisedTotal
                FROM    collateral.ProjectUnits pu
                WHERE   pu.CollateralMasterId = e.CollateralMasterId
            ) ua
WHERE       e.AppraisedCollateralType = 'PRJ'
  AND       ua.AppraisedTotal IS NOT NULL
  AND       e.AppraisalValue = pd.ProjectSellingPrice;

PRINT CONCAT('Corrected AppraisalValue on ', @@ROWCOUNT, ' block-project engagement row(s).');

-- PRJ engagements deliberately left alone: no unit priced yet, or the row already holds the
-- appraised total rather than the list price.
DECLARE @Skipped int = (
    SELECT COUNT(*)
    FROM        collateral.CollateralEngagements e
    INNER JOIN  appraisal.Appraisals a ON a.Id = e.AppraisalId AND a.IsDeleted = 0
    WHERE       e.AppraisedCollateralType = 'PRJ'
      AND NOT EXISTS (
              SELECT 1
              FROM   collateral.ProjectUnits pu
              WHERE  pu.CollateralMasterId = e.CollateralMasterId
                AND  pu.LastAppraisedValue IS NOT NULL
          )
);
PRINT CONCAT(@Skipped, ' block-project engagement(s) skipped (no unit priced yet) — expected, not an error.');

COMMIT TRANSACTION;
