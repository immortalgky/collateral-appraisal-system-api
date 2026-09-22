-- ============================================================
-- Data fix: PricingAnalysisMethods.Role backfill, plus a real BuildingCost method for every
-- Cost-approach WQS/SaleGrid/DirectComparison/Leasehold/ProfitRent method that already carries
-- a building value (HasBuildingValue = 1).
--
-- Item B+C (Cost = sum of role-tagged components; WQS/SAG/DC "include building" links a
-- BuildingCost method). Ordering: DbUp runs after every EF migration, so
-- PricingAnalysisMethods.Role/LinkedMethodId already exist.
--
-- Role rules (see PricingAnalysisMethod.Role / PricingAnalysisApproach.AddMethod):
--   - Methods outside a Cost approach: left NULL. Never touched by this script.
--   - MachineryCost, BuildingCost (Cost approach): Role = their own fixed component.
--   - WQS / SaleGrid / DirectComparison / Leasehold / ProfitRent (Cost approach):
--       HasBuildingValue = 1 -> LandAndBuilding, otherwise (0, or no PricingFinalValues row at
--       all) -> Land. A method with no PricingFinalValues row has never been saved with a
--       building value, so "no building" is the correct, not just convenient, default.
--
-- BuildingCost creation, per the user's decision (2026-09-19): sourced from the SAME real
-- construction-cost and depreciation data the group's Building Cost panel already totals
-- (appraisal.BuildingDepreciationDetails via appraisal.BuildingAppraisalDetails), NOT a
-- synthetic zero-value row. The aggregation below is byte-for-byte the same formula as
-- PricingPropertyDataService.BuildingCostSql (Modules/Appraisal/Appraisal/Application/Services/
-- PricingPropertyDataService.cs) — keep both in sync if that formula ever changes.
--
-- Skip rule: when a group's computed building-cost total is exactly 0 (no building in the group
-- has an override or any depreciation rows), no BuildingCost method is created and the source
-- method's Role is still set to LandAndBuilding (its own FinalValue/BuildingValue already holds
-- the combined figure from before this script ran — nothing is lost), but there is nothing to
-- link. This matches the live LinkOrCreateBuildingCostMethod behaviour: no method beats a
-- zero-value one.
--
-- Idempotent: every statement is guarded so a second run touches nothing further. The
-- BuildingCost-creation step in particular guards on "this approach has no BuildingCost method
-- yet", so re-running never creates a duplicate.
-- ============================================================

-- Step 1: Role = Machinery for existing MachineryCost methods under a Cost approach.
UPDATE pam
SET pam.[Role] = 'Machinery'
FROM [appraisal].[PricingAnalysisMethods] pam
JOIN [appraisal].[PricingAnalysisApproaches] paa ON paa.[Id] = pam.[ApproachId]
WHERE paa.[ApproachType] = 'Cost'
  AND pam.[MethodType] = 'MachineryCost'
  AND pam.[Role] IS NULL;

-- Step 2: Role = Building for existing BuildingCost methods under a Cost approach (the method
-- type predates this item being wired up, so this is expected to touch very few rows, if any).
UPDATE pam
SET pam.[Role] = 'Building'
FROM [appraisal].[PricingAnalysisMethods] pam
JOIN [appraisal].[PricingAnalysisApproaches] paa ON paa.[Id] = pam.[ApproachId]
WHERE paa.[ApproachType] = 'Cost'
  AND pam.[MethodType] = 'BuildingCost'
  AND pam.[Role] IS NULL;

-- Step 3: Role = LandAndBuilding where the method already carries a building value.
UPDATE pam
SET pam.[Role] = 'LandAndBuilding'
FROM [appraisal].[PricingAnalysisMethods] pam
JOIN [appraisal].[PricingAnalysisApproaches] paa ON paa.[Id] = pam.[ApproachId]
JOIN [appraisal].[PricingFinalValues] pfv ON pfv.[PricingMethodId] = pam.[Id]
WHERE paa.[ApproachType] = 'Cost'
  AND pam.[MethodType] IN ('WQS', 'SaleGrid', 'DirectComparison', 'Leasehold', 'ProfitRent')
  AND pfv.[HasBuildingValue] = 1
  AND pam.[Role] IS NULL;

-- Step 4: Role = Land for every other Cost-approach method not yet tagged (no building value, or
-- no PricingFinalValues row at all).
UPDATE pam
SET pam.[Role] = 'Land'
FROM [appraisal].[PricingAnalysisMethods] pam
JOIN [appraisal].[PricingAnalysisApproaches] paa ON paa.[Id] = pam.[ApproachId]
WHERE paa.[ApproachType] = 'Cost'
  AND pam.[MethodType] IN ('WQS', 'SaleGrid', 'DirectComparison', 'Leasehold', 'ProfitRent')
  AND pam.[Role] IS NULL;

-- Step 5: link each LandAndBuilding-tagged method to its approach's BuildingCost method,
-- creating one — from the group's real depreciation schedule — where the approach doesn't
-- already have one. Skips groups whose building-cost total is 0 (nothing to link).
DECLARE @NewBuildingCostMethods TABLE (
    NewMethodId       UNIQUEIDENTIFIER NOT NULL,
    ApproachId        UNIQUEIDENTIFIER NOT NULL,
    BuildingCostValue DECIMAL(18, 2)   NOT NULL
);

INSERT INTO @NewBuildingCostMethods (NewMethodId, ApproachId, BuildingCostValue)
SELECT NEWID(), t.ApproachId, t.BuildingCostValue
FROM (
    SELECT paa.[Id] AS ApproachId, pa.[AnchorId] AS PropertyGroupId,
           ISNULL((
               SELECT SUM(x.FinalCostValue)
               FROM (
                   -- Same aggregation as PricingPropertyDataService.BuildingCostSql: one row per
                   -- building, appraiser override wins, else the depreciation schedule rounded to
                   -- the nearest 1,000; summed across the group. Mixed-family groups (Condo/MAC
                   -- properties alongside Land/Building ones) are naturally excluded — only
                   -- Land/Building-family properties have a BuildingAppraisalDetails row at all.
                   SELECT COALESCE(bad.[FinalCostValueOverride], ROUND(SUM(bdd.[PriceAfterDepreciation]), -3)) AS FinalCostValue
                   FROM [appraisal].[BuildingAppraisalDetails] bad
                   INNER JOIN [appraisal].[AppraisalProperties] ap ON ap.[Id] = bad.[AppraisalPropertyId]
                   INNER JOIN [appraisal].[PropertyGroupItems] pgi ON pgi.[AppraisalPropertyId] = ap.[Id]
                   LEFT JOIN [appraisal].[BuildingDepreciationDetails] bdd ON bdd.[BuildingAppraisalDetailId] = bad.[Id]
                   WHERE pgi.[PropertyGroupId] = pa.[AnchorId]
                   GROUP BY bad.[Id], bad.[FinalCostValueOverride]
               ) x
           ), 0) AS BuildingCostValue
    FROM [appraisal].[PricingAnalysisMethods] pam
    JOIN [appraisal].[PricingAnalysisApproaches] paa ON paa.[Id] = pam.[ApproachId]
    JOIN [appraisal].[PricingAnalysis] pa ON pa.[Id] = paa.[PricingAnalysisId] AND pa.[SubjectType] = 0
    WHERE pam.[Role] = 'LandAndBuilding'
      AND pam.[LinkedMethodId] IS NULL
      AND NOT EXISTS (
          SELECT 1 FROM [appraisal].[PricingAnalysisMethods] bc
          WHERE bc.[ApproachId] = pam.[ApproachId] AND bc.[MethodType] = 'BuildingCost'
      )
    GROUP BY paa.[Id], pa.[AnchorId]
) t
WHERE t.BuildingCostValue > 0;

INSERT INTO [appraisal].[PricingAnalysisMethods]
    ([Id], [ApproachId], [MethodType], [MethodValue], [ValuePerUnit], [UnitType], [IsSelected], [Role], [LinkedMethodId], [CreatedAt], [CreatedBy])
SELECT n.[NewMethodId], n.[ApproachId], 'BuildingCost', n.[BuildingCostValue], NULL, 'PerUnit', 0, 'Building', NULL, GETDATE(), N'SYSTEM'
FROM @NewBuildingCostMethods n;

INSERT INTO [appraisal].[PricingFinalValues]
    ([Id], [PricingMethodId], [FinalValue], [IncludeLandArea], [HasBuildingValue], [CreatedAt], [CreatedBy])
SELECT NEWID(), n.[NewMethodId], n.[BuildingCostValue], 1, 0, GETDATE(), N'SYSTEM'
FROM @NewBuildingCostMethods n;

-- Point every LandAndBuilding-tagged method at its approach's BuildingCost method — the one just
-- created above, or one that already existed (covers the case where a stray/manually-added
-- BuildingCost method already sat in the approach before this script ran).
UPDATE pam
SET pam.[LinkedMethodId] = bc.[Id]
FROM [appraisal].[PricingAnalysisMethods] pam
JOIN [appraisal].[PricingAnalysisMethods] bc
    ON bc.[ApproachId] = pam.[ApproachId] AND bc.[MethodType] = 'BuildingCost'
WHERE pam.[Role] = 'LandAndBuilding'
  AND pam.[LinkedMethodId] IS NULL;

-- The linked BuildingCost method is excluded from the Cost rollup (the source method's own total
-- already folds its value in) — matching PricingAnalysisApproach.LinkOrCreateBuildingCostMethod,
-- which deselects it only when the linked source is itself SELECTED. A BuildingCost that was the
-- approach's selected method while a building-inclusive alternative sat unselected keeps its
-- selection, or the approach would be left with nothing selected.
UPDATE bc
SET bc.[IsSelected] = 0
FROM [appraisal].[PricingAnalysisMethods] bc
JOIN [appraisal].[PricingAnalysisMethods] pam
    ON pam.[LinkedMethodId] = bc.[Id]
WHERE bc.[IsSelected] = 1
  AND pam.[IsSelected] = 1;
