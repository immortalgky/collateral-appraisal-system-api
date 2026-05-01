CREATE
OR ALTER
VIEW appraisal.vw_HypothesisAnalysisSummary AS
SELECT ha.Id,
       ha.PricingMethodId,
       ha.Variant,
       -- Land & Building outputs (FSD C-series, prefixed Lb_ to disambiguate from Condo columns)
       ha.LandBuildingSummary_TotalArea                AS Lb_TotalArea,               -- FSD C01
       ha.LandBuildingSummary_SellingAreaPercent       AS Lb_SellingAreaPercent,       -- FSD C02
       ha.LandBuildingSummary_SellingArea              AS Lb_SellingArea,              -- FSD C03
       ha.LandBuildingSummary_TotalRevenue             AS Lb_TotalRevenue,             -- FSD C15
       ha.LandBuildingSummary_EstSalesPeriod           AS Lb_EstSalesPeriod,           -- FSD C16
       ha.LandBuildingSummary_TotalUnits               AS Lb_TotalUnits,               -- FSD C17
       ha.LandBuildingSummary_EstimatedDurationMonths  AS Lb_EstimatedDurationMonths,  -- FSD C18
       ha.LandBuildingSummary_TotalProjectDevCost      AS Lb_TotalProjectDevCost,      -- FSD C38
       ha.LandBuildingSummary_TotalProjectCost         AS Lb_TotalProjectCost,         -- FSD C64
       ha.LandBuildingSummary_TotalGovTax              AS Lb_TotalGovTax,              -- FSD C72
       ha.LandBuildingSummary_RiskPremiumAmount        AS Lb_RiskPremiumAmount,        -- FSD C75
       ha.LandBuildingSummary_TotalDevCostsAndExpenses AS Lb_TotalDevCostsAndExpenses, -- FSD C76
       ha.LandBuildingSummary_CurrentPropertyValue     AS Lb_CurrentPropertyValue,     -- FSD C77
       ha.LandBuildingSummary_DiscountRate             AS Lb_DiscountRate,             -- FSD C78
       ha.LandBuildingSummary_DiscountRateFactor       AS Lb_DiscountRateFactor,       -- FSD C79
       ha.LandBuildingSummary_FinalPropertyValue       AS Lb_FinalPropertyValue,       -- FSD C80
       ha.LandBuildingSummary_TotalAssetValueRounded   AS Lb_TotalAssetValueRounded,   -- FSD C81
       ha.LandBuildingSummary_TotalAssetValuePerSqWa   AS Lb_TotalAssetValuePerSqWa,   -- FSD C82
       -- Condominium outputs (FSD E-series, prefixed Condo_)
       ha.CondominiumSummary_AreaTitleDeed             AS Condo_AreaTitleDeed,         -- FSD E01
       ha.CondominiumSummary_FAR                       AS Condo_FAR,                   -- FSD E03
       ha.CondominiumSummary_TotalBuildingArea         AS Condo_TotalBuildingArea,     -- FSD E05
       ha.CondominiumSummary_IndoorSalesArea           AS Condo_IndoorSalesArea,       -- FSD E09
       ha.CondominiumSummary_TotalRevenue              AS Condo_TotalRevenue,          -- FSD E13
       ha.CondominiumSummary_EstSalesDurationMonths    AS Condo_EstSalesDurationMonths,-- FSD E14
       ha.CondominiumSummary_SetAvgRoomSizeUnits       AS Condo_SetAvgRoomSizeUnits,   -- FSD E18
       ha.CondominiumSummary_TotalHardCost             AS Condo_TotalHardCost,         -- FSD E27
       ha.CondominiumSummary_TotalSoftCost             AS Condo_TotalSoftCost,         -- FSD E45
       ha.CondominiumSummary_TotalGovTax               AS Condo_TotalGovTax,           -- FSD E50
       ha.CondominiumSummary_RiskProfitTotal           AS Condo_RiskProfitTotal,       -- FSD E52
       ha.CondominiumSummary_TotalDevCosts             AS Condo_TotalDevCosts,         -- FSD E53
       ha.CondominiumSummary_TotalRemainingValue       AS Condo_TotalRemainingValue,   -- FSD E54
       ha.CondominiumSummary_DiscountRate              AS Condo_DiscountRate,          -- FSD E55
       ha.CondominiumSummary_DiscountRateFactor        AS Condo_DiscountRateFactor,    -- FSD E56
       ha.CondominiumSummary_FinalRemainingValue       AS Condo_FinalRemainingValue,   -- FSD E57
       ha.CondominiumSummary_TotalAssetValueRounded    AS Condo_TotalAssetValueRounded,-- FSD E58
       ha.CondominiumSummary_TotalAssetValuePerSqM     AS Condo_TotalAssetValuePerSqM, -- FSD E59
       -- Upload counts
       (SELECT COUNT(*)
        FROM appraisal.HypothesisUnitDetailUploads u
        WHERE u.HypothesisAnalysisId = ha.Id)              AS TotalUploads,
       (SELECT COUNT(*)
        FROM appraisal.HypothesisUnitDetailUploads u
        WHERE u.HypothesisAnalysisId = ha.Id
          AND u.IsActive = 1)                              AS ActiveUploadCount,
       -- Row counts
       (SELECT COUNT(*)
        FROM appraisal.HypothesisLandBuildingUnitRows r
        WHERE r.HypothesisAnalysisId = ha.Id)              AS LandBuildingRowCount,
       (SELECT COUNT(*)
        FROM appraisal.HypothesisCondominiumUnitRows r
        WHERE r.HypothesisAnalysisId = ha.Id)              AS CondominiumRowCount,
       -- Cost item count
       (SELECT COUNT(*)
        FROM appraisal.HypothesisCostItems ci
        WHERE ci.HypothesisAnalysisId = ha.Id)             AS CostItemCount,
       ha.CreatedAt,
       ha.UpdatedAt
FROM appraisal.HypothesisAnalyses ha;
