CREATE
OR ALTER
VIEW appraisal.vw_HypothesisAnalysisSummary AS
SELECT ha.Id,
       ha.PricingMethodId,
       ha.Variant,
       -- Land & Building outputs (C-series)
       ha.C01TotalArea,
       ha.C02SellingAreaPercent,
       ha.C03SellingArea,
       ha.C15TotalRevenue,
       ha.C16EstSalesPeriod,
       ha.C17TotalUnits,
       ha.C18EstimatedDurationMonths,
       ha.C38TotalProjectDevCost,
       ha.C64TotalProjectCost,
       ha.C72TotalGovTax,
       ha.C75RiskPremiumAmount,
       ha.C76TotalDevCostsAndExpenses,
       ha.C77CurrentPropertyValue,
       ha.C78DiscountRate,
       ha.C79DiscountRateFactor,
       ha.C80FinalPropertyValue,
       ha.C81TotalAssetValueRounded,
       ha.C82TotalAssetValuePerSqWa,
       -- Condominium outputs (E-series)
       ha.E01AreaTitleDeed,
       ha.E03FAR,
       ha.E05TotalBuildingArea,
       ha.E09IndoorSalesArea,
       ha.E13TotalRevenue,
       ha.E14EstSalesDurationMonths,
       ha.E18SetAvgRoomSizeUnits,
       ha.E27TotalHardCost,
       ha.E45TotalSoftCost,
       ha.E50TotalGovTax,
       ha.E52RiskProfitTotal,
       ha.E53TotalDevCosts,
       ha.E54TotalRemainingValue,
       ha.E55DiscountRate,
       ha.E56DiscountRateFactor,
       ha.E57FinalRemainingValue,
       ha.E58TotalAssetValueRounded,
       ha.E59TotalAssetValuePerSqM,
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
