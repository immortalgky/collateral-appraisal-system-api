-- ============================================================
-- Data fix: give calculated block unit prices their forced-sale price.
--
-- A block's forced-sale value is now the SUM of its units' ForceSellingPrice (it used to be
-- total x rate). Project.CalculateUnitPrices used to leave ForceSellingPrice NULL when the pricing
-- assumption's ForceSalePercentage was blank, so those blocks would show forced sale 0 in Decision
-- Summary as soon as this release is live. Calculate now always applies a percentage; this fills in
-- the rows calculated before that.
--
-- Only appraisals still in progress are touched: not Completed or Cancelled, and not already approved by the
-- committee (CompletedAt is stamped at approval, which can come before the status flips to Completed). A closed
-- block keeps the prices it was approved with.
--
-- Only rows that have an appraisal value (TotalAppraisalValueRounded) and no ForceSellingPrice are
-- touched. Percentage = the project's pricing assumption when it is within 0..100 (0 is a real value), else
-- the system default (common.SystemConfigurations 'ForceSaleRateDefaultPct', active) when within 0..100,
-- else 70 -- the same rule Calculate uses (Project.ResolveForceSalePercentage). Value = TotalAppraisalValueRounded x % / 100 rounded to the nearest 1,000
-- (ROUND() is half-away-from-zero, matching MidpointRounding.AwayFromZero).
--
-- This does NOT refresh appraisal.ValuationAnalyses: that row moves the next time the block's prices
-- are calculated or imported, or its decision summary is saved. Not an EF migration; no audit columns
-- are touched. Idempotent: a second run finds no NULL ForceSellingPrice left to fill.
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @ConfiguredPct decimal(18, 6) =
    (SELECT TOP (1) TRY_CONVERT(decimal(18, 6), sc.[Value])
     FROM [common].[SystemConfigurations] sc
     WHERE sc.[Key] = N'ForceSaleRateDefaultPct' AND sc.[IsActive] = 1);
DECLARE @DefaultPct decimal(18, 6) =
    CASE WHEN @ConfiguredPct BETWEEN 0 AND 100 THEN @ConfiguredPct ELSE 70 END;

UPDATE pup
SET    pup.[ForceSellingPrice] =
           ROUND(pup.[TotalAppraisalValueRounded] * CASE WHEN ppa.[ForceSalePercentage] BETWEEN 0 AND 100 THEN ppa.[ForceSalePercentage] ELSE @DefaultPct END / 100.0 / 1000.0, 0) * 1000
FROM   [appraisal].[ProjectUnitPrices] pup
       JOIN [appraisal].[ProjectUnits] pu ON pu.[Id] = pup.[ProjectUnitId]
       JOIN [appraisal].[Projects] p ON p.[Id] = pu.[ProjectId]
       JOIN [appraisal].[Appraisals] a ON a.[Id] = p.[AppraisalId]
       LEFT JOIN [appraisal].[ProjectPricingAssumptions] ppa ON ppa.[ProjectId] = pu.[ProjectId]
WHERE  pup.[TotalAppraisalValueRounded] IS NOT NULL
  AND  pup.[ForceSellingPrice] IS NULL
  AND  a.[Status] NOT IN (N'Completed', N'Cancelled')
  AND  a.[CompletedAt] IS NULL;
GO
