using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.CostItems;
using Appraisal.Domain.Appraisals.Hypothesis.Summaries;
using Appraisal.Domain.Appraisals.Hypothesis.Uploads;

namespace Appraisal.Infrastructure.Configurations;

public class HypothesisAnalysisConfiguration : IEntityTypeConfiguration<HypothesisAnalysis>
{
    public void Configure(EntityTypeBuilder<HypothesisAnalysis> builder)
    {
        builder.ToTable("HypothesisAnalyses");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(h => h.PricingMethodId).IsRequired();
        builder.HasIndex(h => h.PricingMethodId).IsUnique();

        builder.Property(h => h.Variant)
            .IsRequired()
            .HasConversion<int>();

        // ── Owned entity: LandBuildingSummary ────────────────────────────
        builder.OwnsOne(h => h.LandBuildingSummary, lb =>
        {
            lb.Property(s => s.C01TotalArea).HasPrecision(13, 2).HasColumnName("C01TotalArea");
            lb.Property(s => s.C02SellingAreaPercent).HasPrecision(5, 2).HasColumnName("C02SellingAreaPercent");
            lb.Property(s => s.C03SellingArea).HasPrecision(13, 2).HasColumnName("C03SellingArea");
            lb.Property(s => s.C10PublicUtilityAreaPercent).HasPrecision(5, 2).HasColumnName("C10PublicUtilityAreaPercent");
            lb.Property(s => s.C10APublicUtilityArea).HasPrecision(13, 2).HasColumnName("C10APublicUtilityArea");
            lb.Property(s => s.C15TotalRevenue).HasPrecision(17, 2).HasColumnName("C15TotalRevenue");
            lb.Property(s => s.C16EstSalesPeriod).HasColumnName("C16EstSalesPeriod");
            lb.Property(s => s.C17TotalUnits).HasColumnName("C17TotalUnits");
            lb.Property(s => s.C18EstimatedDurationMonths).HasColumnName("C18EstimatedDurationMonths");
            lb.Property(s => s.C27PublicUtilityRatePerSqWa).HasPrecision(17, 2).HasColumnName("C27PublicUtilityRatePerSqWa");
            lb.Property(s => s.C28PublicUtilityArea).HasPrecision(13, 2).HasColumnName("C28PublicUtilityArea");
            lb.Property(s => s.C29PublicUtilityCost).HasPrecision(17, 2).HasColumnName("C29PublicUtilityCost");
            lb.Property(s => s.C30PublicUtilityCostRatio).HasPrecision(5, 2).HasColumnName("C30PublicUtilityCostRatio");
            lb.Property(s => s.C31LandFillingRatePerSqWa).HasPrecision(17, 2).HasColumnName("C31LandFillingRatePerSqWa");
            lb.Property(s => s.C32LandFillingArea).HasPrecision(13, 2).HasColumnName("C32LandFillingArea");
            lb.Property(s => s.C33LandFillingCost).HasPrecision(17, 2).HasColumnName("C33LandFillingCost");
            lb.Property(s => s.C34LandFillingCostRatio).HasPrecision(5, 2).HasColumnName("C34LandFillingCostRatio");
            lb.Property(s => s.C35ContingencyPercent).HasPrecision(5, 2).HasColumnName("C35ContingencyPercent");
            lb.Property(s => s.C36ContingencyAmount).HasPrecision(17, 2).HasColumnName("C36ContingencyAmount");
            lb.Property(s => s.C37ContingencyRatio).HasPrecision(5, 2).HasColumnName("C37ContingencyRatio");
            lb.Property(s => s.C38TotalProjectDevCost).HasPrecision(17, 2).HasColumnName("C38TotalProjectDevCost");
            lb.Property(s => s.C39TotalDevCostRatio).HasPrecision(5, 2).HasColumnName("C39TotalDevCostRatio");
            lb.Property(s => s.C40EstConstructionPeriod).HasColumnName("C40EstConstructionPeriod");
            lb.Property(s => s.C41TotalUnits).HasColumnName("C41TotalUnits");
            lb.Property(s => s.C42EstimatedDurationMonths).HasColumnName("C42EstimatedDurationMonths");
            lb.Property(s => s.C44AllocationPermitFee).HasPrecision(17, 2).HasColumnName("C44AllocationPermitFee");
            lb.Property(s => s.C45AllocationPermitFeeRatio).HasPrecision(5, 2).HasColumnName("C45AllocationPermitFeeRatio");
            lb.Property(s => s.C46LandTitleFeePerPlot).HasPrecision(17, 2).HasColumnName("C46LandTitleFeePerPlot");
            lb.Property(s => s.C47TotalPlots).HasColumnName("C47TotalPlots");
            lb.Property(s => s.C48LandTitleFeeTotal).HasPrecision(17, 2).HasColumnName("C48LandTitleFeeTotal");
            lb.Property(s => s.C49LandTitleFeeRatio).HasPrecision(5, 2).HasColumnName("C49LandTitleFeeRatio");
            lb.Property(s => s.C50ProfessionalFeePerMonth).HasPrecision(17, 2).HasColumnName("C50ProfessionalFeePerMonth");
            lb.Property(s => s.C51ProfessionalFeeMonths).HasColumnName("C51ProfessionalFeeMonths");
            lb.Property(s => s.C52ProfessionalFeeTotal).HasPrecision(17, 2).HasColumnName("C52ProfessionalFeeTotal");
            lb.Property(s => s.C53ProfessionalFeeRatio).HasPrecision(5, 2).HasColumnName("C53ProfessionalFeeRatio");
            lb.Property(s => s.C54AdminCostPerMonth).HasPrecision(17, 2).HasColumnName("C54AdminCostPerMonth");
            lb.Property(s => s.C55AdminCostMonths).HasColumnName("C55AdminCostMonths");
            lb.Property(s => s.C56AdminCostTotal).HasPrecision(17, 2).HasColumnName("C56AdminCostTotal");
            lb.Property(s => s.C57AdminCostRatio).HasPrecision(5, 2).HasColumnName("C57AdminCostRatio");
            lb.Property(s => s.C58SellingAdvPercent).HasPrecision(5, 2).HasColumnName("C58SellingAdvPercent");
            lb.Property(s => s.C59SellingAdvTotal).HasPrecision(17, 2).HasColumnName("C59SellingAdvTotal");
            lb.Property(s => s.C60SellingAdvRatio).HasPrecision(5, 2).HasColumnName("C60SellingAdvRatio");
            lb.Property(s => s.C61ProjectContingencyPercent).HasPrecision(5, 2).HasColumnName("C61ProjectContingencyPercent");
            lb.Property(s => s.C62ProjectContingencyAmount).HasPrecision(17, 2).HasColumnName("C62ProjectContingencyAmount");
            lb.Property(s => s.C63ProjectContingencyRatio).HasPrecision(5, 2).HasColumnName("C63ProjectContingencyRatio");
            lb.Property(s => s.C64TotalProjectCost).HasPrecision(17, 2).HasColumnName("C64TotalProjectCost");
            lb.Property(s => s.C65TotalProjectCostRatio).HasPrecision(5, 2).HasColumnName("C65TotalProjectCostRatio");
            lb.Property(s => s.C66TransferFeePercent).HasPrecision(5, 2).HasColumnName("C66TransferFeePercent");
            lb.Property(s => s.C67TransferFeeAmount).HasPrecision(17, 2).HasColumnName("C67TransferFeeAmount");
            lb.Property(s => s.C68TransferFeeRatio).HasPrecision(5, 2).HasColumnName("C68TransferFeeRatio");
            lb.Property(s => s.C69SpecificBizTaxPercent).HasPrecision(5, 2).HasColumnName("C69SpecificBizTaxPercent");
            lb.Property(s => s.C70SpecificBizTaxAmount).HasPrecision(17, 2).HasColumnName("C70SpecificBizTaxAmount");
            lb.Property(s => s.C71SpecificBizTaxRatio).HasPrecision(5, 2).HasColumnName("C71SpecificBizTaxRatio");
            lb.Property(s => s.C72TotalGovTax).HasPrecision(17, 2).HasColumnName("C72TotalGovTax");
            lb.Property(s => s.C73TotalGovTaxRatio).HasPrecision(5, 2).HasColumnName("C73TotalGovTaxRatio");
            lb.Property(s => s.C74RiskPremiumPercent).HasPrecision(5, 2).HasColumnName("C74RiskPremiumPercent");
            lb.Property(s => s.C75RiskPremiumAmount).HasPrecision(17, 2).HasColumnName("C75RiskPremiumAmount");
            lb.Property(s => s.C76TotalDevCostsAndExpenses).HasPrecision(17, 2).HasColumnName("C76TotalDevCostsAndExpenses");
            lb.Property(s => s.C77CurrentPropertyValue).HasPrecision(17, 2).HasColumnName("C77CurrentPropertyValue");
            lb.Property(s => s.C78DiscountRate).HasPrecision(5, 2).HasColumnName("C78DiscountRate");
            lb.Property(s => s.C79DiscountRateFactor).HasPrecision(18, 10).HasColumnName("C79DiscountRateFactor");
            lb.Property(s => s.C80FinalPropertyValue).HasPrecision(17, 2).HasColumnName("C80FinalPropertyValue");
            lb.Property(s => s.C81TotalAssetValueRounded).HasPrecision(17, 2).HasColumnName("C81TotalAssetValueRounded");
            lb.Property(s => s.C82TotalAssetValuePerSqWa).HasPrecision(17, 2).HasColumnName("C82TotalAssetValuePerSqWa");
            lb.Property(s => s.Remark).HasMaxLength(4000).HasColumnName("LB_Remark");
        });

        // ── Owned entity: CondominiumSummary ─────────────────────────────
        builder.OwnsOne(h => h.CondominiumSummary, cs =>
        {
            cs.Property(s => s.E01AreaTitleDeed).HasPrecision(7, 2).HasColumnName("E01AreaTitleDeed");
            cs.Property(s => s.E02AreaSqM).HasPrecision(7, 2).HasColumnName("E02AreaSqM");
            cs.Property(s => s.E03FAR).HasPrecision(7, 2).HasColumnName("E03FAR");
            cs.Property(s => s.E04ConstructionAreaCityPlan).HasPrecision(7, 2).HasColumnName("E04ConstructionAreaCityPlan");
            cs.Property(s => s.E05TotalBuildingArea).HasPrecision(7, 2).HasColumnName("E05TotalBuildingArea");
            cs.Property(s => s.E06CommonAreaPercent).HasPrecision(5, 2).HasColumnName("E06CommonAreaPercent");
            cs.Property(s => s.E07CommonArea).HasPrecision(7, 2).HasColumnName("E07CommonArea");
            cs.Property(s => s.E08IndoorSalesAreaPercent).HasPrecision(5, 2).HasColumnName("E08IndoorSalesAreaPercent");
            cs.Property(s => s.E09IndoorSalesArea).HasPrecision(7, 2).HasColumnName("E09IndoorSalesArea");
            cs.Property(s => s.E10ProjectSalesArea).HasPrecision(7, 2).HasColumnName("E10ProjectSalesArea");
            cs.Property(s => s.E11AveragePricePerSqM).HasPrecision(17, 2).HasColumnName("E11AveragePricePerSqM");
            cs.Property(s => s.E12TotalProjectSellingPrice).HasPrecision(17, 2).HasColumnName("E12TotalProjectSellingPrice");
            cs.Property(s => s.E13TotalRevenue).HasPrecision(17, 2).HasColumnName("E13TotalRevenue");
            cs.Property(s => s.E14EstSalesDurationMonths).HasColumnName("E14EstSalesDurationMonths");
            cs.Property(s => s.E15CondoBuildingCostPerSqM).HasPrecision(17, 2).HasColumnName("E15CondoBuildingCostPerSqM");
            cs.Property(s => s.E16BuildingArea).HasPrecision(7, 2).HasColumnName("E16BuildingArea");
            cs.Property(s => s.E17CondoBuildingCostTotal).HasPrecision(17, 2).HasColumnName("E17CondoBuildingCostTotal");
            cs.Property(s => s.E18SetAvgRoomSizeUnits).HasColumnName("E18SetAvgRoomSizeUnits");
            cs.Property(s => s.E19AvgIndoorSalesAreaPerUnit).HasPrecision(7, 2).HasColumnName("E19AvgIndoorSalesAreaPerUnit");
            cs.Property(s => s.E20FurniturePerUnit).HasPrecision(17, 2).HasColumnName("E20FurniturePerUnit");
            cs.Property(s => s.E21FurnitureQuantity).HasColumnName("E21FurnitureQuantity");
            cs.Property(s => s.E22FurnitureTotal).HasPrecision(17, 2).HasColumnName("E22FurnitureTotal");
            cs.Property(s => s.E23ExternalUtilities).HasPrecision(17, 2).HasColumnName("E23ExternalUtilities");
            cs.Property(s => s.E24ExternalUtilitiesTotal).HasPrecision(17, 2).HasColumnName("E24ExternalUtilitiesTotal");
            cs.Property(s => s.E25HardCostContingencyPercent).HasPrecision(5, 2).HasColumnName("E25HardCostContingencyPercent");
            cs.Property(s => s.E26HardCostContingencyAmount).HasPrecision(17, 2).HasColumnName("E26HardCostContingencyAmount");
            cs.Property(s => s.E27TotalHardCost).HasPrecision(17, 2).HasColumnName("E27TotalHardCost");
            cs.Property(s => s.E28EstConstructionPeriodMonths).HasColumnName("E28EstConstructionPeriodMonths");
            cs.Property(s => s.E29ProfessionalFeePerMonth).HasPrecision(17, 2).HasColumnName("E29ProfessionalFeePerMonth");
            cs.Property(s => s.E30ProfessionalFeeMonths).HasColumnName("E30ProfessionalFeeMonths");
            cs.Property(s => s.E31ProfessionalFeeTotal).HasPrecision(17, 2).HasColumnName("E31ProfessionalFeeTotal");
            cs.Property(s => s.E32AdminCostPerMonth).HasPrecision(17, 2).HasColumnName("E32AdminCostPerMonth");
            cs.Property(s => s.E33AdminCostMonths).HasColumnName("E33AdminCostMonths");
            cs.Property(s => s.E34AdminCostTotal).HasPrecision(17, 2).HasColumnName("E34AdminCostTotal");
            cs.Property(s => s.E35SellingAdvPercent).HasPrecision(5, 2).HasColumnName("E35SellingAdvPercent");
            cs.Property(s => s.E36SellingAdvTotal).HasPrecision(17, 2).HasColumnName("E36SellingAdvTotal");
            cs.Property(s => s.E37TitleDeedFee).HasPrecision(17, 2).HasColumnName("E37TitleDeedFee");
            cs.Property(s => s.E38TitleDeedFeeTotal).HasPrecision(17, 2).HasColumnName("E38TitleDeedFeeTotal");
            cs.Property(s => s.E39EIACost).HasPrecision(17, 2).HasColumnName("E39EIACost");
            cs.Property(s => s.E40EIACostTotal).HasPrecision(17, 2).HasColumnName("E40EIACostTotal");
            cs.Property(s => s.E41CondoRegistrationFee).HasPrecision(17, 2).HasColumnName("E41CondoRegistrationFee");
            cs.Property(s => s.E42CondoRegistrationFeeTotal).HasPrecision(17, 2).HasColumnName("E42CondoRegistrationFeeTotal");
            cs.Property(s => s.E43OtherExpensesPercent).HasPrecision(5, 2).HasColumnName("E43OtherExpensesPercent");
            cs.Property(s => s.E44OtherExpensesTotal).HasPrecision(17, 2).HasColumnName("E44OtherExpensesTotal");
            cs.Property(s => s.E45TotalSoftCost).HasPrecision(17, 2).HasColumnName("E45TotalSoftCost");
            cs.Property(s => s.E46TransferFeePercent).HasPrecision(5, 2).HasColumnName("E46TransferFeePercent");
            cs.Property(s => s.E47TransferFeeTotal).HasPrecision(17, 2).HasColumnName("E47TransferFeeTotal");
            cs.Property(s => s.E48SpecificBizTaxPercent).HasPrecision(5, 2).HasColumnName("E48SpecificBizTaxPercent");
            cs.Property(s => s.E49SpecificBizTaxTotal).HasPrecision(17, 2).HasColumnName("E49SpecificBizTaxTotal");
            cs.Property(s => s.E50TotalGovTax).HasPrecision(17, 2).HasColumnName("E50TotalGovTax");
            cs.Property(s => s.E51RiskProfitPercent).HasPrecision(5, 2).HasColumnName("E51RiskProfitPercent");
            cs.Property(s => s.E52RiskProfitTotal).HasPrecision(17, 2).HasColumnName("E52RiskProfitTotal");
            cs.Property(s => s.E53TotalDevCosts).HasPrecision(17, 2).HasColumnName("E53TotalDevCosts");
            cs.Property(s => s.E54TotalRemainingValue).HasPrecision(17, 2).HasColumnName("E54TotalRemainingValue");
            cs.Property(s => s.E55DiscountRate).HasPrecision(5, 2).HasColumnName("E55DiscountRate");
            cs.Property(s => s.E56DiscountRateFactor).HasPrecision(18, 10).HasColumnName("E56DiscountRateFactor");
            cs.Property(s => s.E57FinalRemainingValue).HasPrecision(17, 2).HasColumnName("E57FinalRemainingValue");
            cs.Property(s => s.E58TotalAssetValueRounded).HasPrecision(17, 2).HasColumnName("E58TotalAssetValueRounded");
            cs.Property(s => s.E59TotalAssetValuePerSqM).HasPrecision(17, 2).HasColumnName("E59TotalAssetValuePerSqM");
            cs.Property(s => s.Remark).HasMaxLength(4000).HasColumnName("Condo_Remark");
        });

        // ── Collections (navigate via aggregate root only) ────────────────
        builder.HasMany(h => h.Uploads)
            .WithOne()
            .HasForeignKey(u => u.HypothesisAnalysisId)
            .OnDelete(DeleteBehavior.Cascade)
            .Metadata.PrincipalToDependent!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(h => h.LandBuildingUnitRows)
            .WithOne()
            .HasForeignKey(r => r.HypothesisAnalysisId)
            .OnDelete(DeleteBehavior.Cascade)
            .Metadata.PrincipalToDependent!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(h => h.CondominiumUnitRows)
            .WithOne()
            .HasForeignKey(r => r.HypothesisAnalysisId)
            .OnDelete(DeleteBehavior.Cascade)
            .Metadata.PrincipalToDependent!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(h => h.CostItems)
            .WithOne()
            .HasForeignKey(i => i.HypothesisAnalysisId)
            .OnDelete(DeleteBehavior.Cascade)
            .Metadata.PrincipalToDependent!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class HypothesisUnitDetailUploadConfiguration : IEntityTypeConfiguration<HypothesisUnitDetailUpload>
{
    public void Configure(EntityTypeBuilder<HypothesisUnitDetailUpload> builder)
    {
        builder.ToTable("HypothesisUnitDetailUploads");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(u => u.HypothesisAnalysisId).IsRequired();
        builder.Property(u => u.FileName).IsRequired().HasMaxLength(500);
        builder.Property(u => u.UploadedAt).IsRequired();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.RowCount).IsRequired();

        builder.HasIndex(u => u.HypothesisAnalysisId);
    }
}

public class LandBuildingUnitRowConfiguration : IEntityTypeConfiguration<LandBuildingUnitRow>
{
    public void Configure(EntityTypeBuilder<LandBuildingUnitRow> builder)
    {
        builder.ToTable("HypothesisLandBuildingUnitRows");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.UploadId).IsRequired();
        builder.Property(r => r.HypothesisAnalysisId).IsRequired();
        builder.Property(r => r.SequenceNumber).IsRequired();

        builder.Property(r => r.PlanNo).HasMaxLength(100);
        builder.Property(r => r.HouseNo).HasMaxLength(100);
        builder.Property(r => r.ModelName).HasMaxLength(200);
        builder.Property(r => r.LandAreaSqWa).HasPrecision(13, 2);
        builder.Property(r => r.SellingPrice).HasPrecision(17, 2);

        builder.HasIndex(r => r.HypothesisAnalysisId);
        builder.HasIndex(r => r.UploadId);
    }
}

public class CondominiumUnitRowConfiguration : IEntityTypeConfiguration<CondominiumUnitRow>
{
    public void Configure(EntityTypeBuilder<CondominiumUnitRow> builder)
    {
        builder.ToTable("HypothesisCondominiumUnitRows");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.UploadId).IsRequired();
        builder.Property(r => r.HypothesisAnalysisId).IsRequired();
        builder.Property(r => r.SequenceNumber).IsRequired();

        builder.Property(r => r.Building).HasMaxLength(100);
        builder.Property(r => r.AptNo).HasMaxLength(100);
        builder.Property(r => r.ModelType).HasMaxLength(200);
        builder.Property(r => r.UsableAreaSqM).HasPrecision(13, 2);
        builder.Property(r => r.SellingPrice).HasPrecision(17, 2);

        builder.HasIndex(r => r.HypothesisAnalysisId);
        builder.HasIndex(r => r.UploadId);
    }
}

public class HypothesisCostItemConfiguration : IEntityTypeConfiguration<HypothesisCostItem>
{
    public void Configure(EntityTypeBuilder<HypothesisCostItem> builder)
    {
        builder.ToTable("HypothesisCostItems");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.HypothesisAnalysisId).IsRequired();
        builder.Property(i => i.Category).IsRequired().HasConversion<int>();
        builder.Property(i => i.Kind).IsRequired().HasConversion<int>();
        builder.Property(i => i.ModelName).HasMaxLength(200);
        builder.Property(i => i.Description).IsRequired().HasMaxLength(500);
        builder.Property(i => i.DisplaySequence).IsRequired();

        builder.Property(i => i.RateAmount).HasPrecision(17, 2);
        builder.Property(i => i.Quantity).HasPrecision(13, 2);
        builder.Property(i => i.Amount).HasPrecision(17, 2);
        builder.Property(i => i.RatePercent).HasPrecision(5, 2);
        builder.Property(i => i.CategoryRatio).HasPrecision(5, 2);

        builder.HasIndex(i => i.HypothesisAnalysisId);
        builder.HasIndex(i => new { i.HypothesisAnalysisId, i.Category });
    }
}
