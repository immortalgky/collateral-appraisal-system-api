-- ============================================================
-- Data fix: tag existing Market-approach methods with a Role.
--
-- The appraiser now declares what a Market method values, using the same Role chip the Cost
-- approach has: "Land" (the method prices the land alone, by area) or "LandAndBuilding" (it prices
-- the whole property as one lump, so no land figure is recorded). See PricingAnalysisMethod.Role and
-- PricingAnalysisApproach.SetMethodRole. Income and Residual stay NULL (whole lump, no land figures).
--
-- Default, the same one AddMethodCommandHandler applies to a newly added Market method:
--   the group has at least one property and EVERY property's PropertyType is 'L' or 'LSL'
--   (leasehold land)  -> 'Land'
--   anything else (B, LB, LSB, LS, U, LSU, MAC, VEH, VES, a mixed group, or a group with no
--   properties) -> 'LandAndBuilding'
-- KEEP IN SYNC with PricingPropertyDataService.DefaultMarketRole.
--
-- Only Role is written. LandArea / LandValue are NOT touched: a Land method fills them on its next
-- save (ApplyLandAreaValue + SyncLandValueWithIndicatedValue), a LandAndBuilding one already holds
-- NULL there, by decision (no backfill of the land figures).
--
-- Scope of the backfill: methods under a Market approach of a PROPERTY-GROUP pricing analysis
-- (SubjectType = 0). Reference analyses (DCF clones, SubjectType NOT IN (0, 1)) have no group and get
-- no role.
--
-- Second statement: reference analyses cloned their Cost source method WITH its Cost role (Land,
-- LandAndBuilding, Building or Machinery), which means nothing in a Market approach and would let the
-- land-value sync overwrite the DCF land override they hold. Clone code now leaves the role NULL
-- (PricingAnalysisApproach.AttachClonedMethod); this clears it on the existing ones, for every
-- non-Cost approach of a REFERENCE analysis (SubjectType NOT IN (0, 1), i.e. neither a property group
-- nor a project model — the same set as PricingAnalysis.IsReference).
--
-- Idempotent: the backfill is WHERE Role IS NULL and the reference clean-up is WHERE Role IS NOT NULL,
-- so a second run touches nothing, and a Role the appraiser has since set on a group method is never
-- overwritten.
-- ============================================================

UPDATE pam
SET pam.[Role] = CASE
        WHEN EXISTS (SELECT 1
                     FROM [appraisal].[PropertyGroupItems] gi
                     WHERE gi.[PropertyGroupId] = pa.[AnchorId])
             AND NOT EXISTS (SELECT 1
                             FROM [appraisal].[PropertyGroupItems] gi
                             JOIN [appraisal].[AppraisalProperties] ap ON ap.[Id] = gi.[AppraisalPropertyId]
                             WHERE gi.[PropertyGroupId] = pa.[AnchorId]
                               AND ISNULL(ap.[PropertyType], '') NOT IN ('L', 'LSL'))
            THEN 'Land'
        ELSE 'LandAndBuilding'
    END
FROM [appraisal].[PricingAnalysisMethods] pam
JOIN [appraisal].[PricingAnalysisApproaches] paa ON paa.[Id] = pam.[ApproachId]
JOIN [appraisal].[PricingAnalysis] pa ON pa.[Id] = paa.[PricingAnalysisId]
WHERE paa.[ApproachType] = 'Market'
  AND pa.[SubjectType] = 0
  AND pam.[Role] IS NULL;

-- Reference analyses: no group, so no land / land-and-building declaration.
UPDATE pam
SET pam.[Role] = NULL
FROM [appraisal].[PricingAnalysisMethods] pam
JOIN [appraisal].[PricingAnalysisApproaches] paa ON paa.[Id] = pam.[ApproachId]
JOIN [appraisal].[PricingAnalysis] pa ON pa.[Id] = paa.[PricingAnalysisId]
WHERE paa.[ApproachType] <> 'Cost'
  AND pa.[SubjectType] NOT IN (0, 1)
  AND pam.[Role] IS NOT NULL;
