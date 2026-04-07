using Appraisal.Domain.ComparativeAnalysis;

namespace Appraisal.Infrastructure.Configurations;

public class PricingAnalysisConfiguration : IEntityTypeConfiguration<PricingAnalysis>
{
    public void Configure(EntityTypeBuilder<PricingAnalysis> builder)
    {
        builder.ToTable("PricingAnalysis");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(p => p.PropertyGroupId).IsRequired();
        builder.HasIndex(p => p.PropertyGroupId).IsUnique();

        builder.Property(p => p.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Draft");

        builder.Property(p => p.FinalAppraisedValue).HasPrecision(18, 2);

        builder.Property(p => p.UseSystemCalc).IsRequired().HasDefaultValue(true);

        builder.HasMany(p => p.Approaches)
            .WithOne()
            .HasForeignKey(a => a.PricingAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PricingAnalysisApproachConfiguration : IEntityTypeConfiguration<PricingAnalysisApproach>
{
    public void Configure(EntityTypeBuilder<PricingAnalysisApproach> builder)
    {
        builder.ToTable("PricingAnalysisApproaches");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.PricingAnalysisId).IsRequired();
        builder.Property(a => a.ApproachType).IsRequired().HasMaxLength(20);
        builder.Property(a => a.ApproachValue).HasPrecision(18, 2);

        builder.HasMany(a => a.Methods)
            .WithOne()
            .HasForeignKey(m => m.ApproachId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.PricingAnalysisId);
    }
}

public class PricingAnalysisMethodConfiguration : IEntityTypeConfiguration<PricingAnalysisMethod>
{
    public void Configure(EntityTypeBuilder<PricingAnalysisMethod> builder)
    {
        builder.ToTable("PricingAnalysisMethods");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(m => m.ApproachId).IsRequired();
        builder.Property(m => m.MethodType).IsRequired().HasMaxLength(50);
        builder.Property(m => m.MethodValue).HasPrecision(18, 2);
        builder.Property(m => m.ValuePerUnit).HasPrecision(18, 2);
        builder.Property(m => m.UnitType).HasMaxLength(20);
        builder.Property(m => m.Remark).HasMaxLength(4000);

        builder.HasMany(m => m.ComparableLinks)
            .WithOne()
            .HasForeignKey(c => c.PricingMethodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Calculations)
            .WithOne()
            .HasForeignKey(c => c.PricingMethodId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comparative Factors (Step 1)
        builder.HasMany(m => m.ComparativeFactors)
            .WithOne()
            .HasForeignKey(f => f.PricingMethodId)
            .OnDelete(DeleteBehavior.Cascade)
            .Metadata.PrincipalToDependent!.SetPropertyAccessMode(PropertyAccessMode.Field);

        // Factor Scores (Step 2) - MOVED from PricingCalculation
        builder.HasMany(m => m.FactorScores)
            .WithOne()
            .HasForeignKey(f => f.PricingMethodId)
            .OnDelete(DeleteBehavior.Cascade)
            .Metadata.PrincipalToDependent!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne(m => m.FinalValue)
            .WithOne()
            .HasForeignKey<PricingFinalValue>(f => f.PricingMethodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.RsqResult)
            .WithOne()
            .HasForeignKey<PricingRsqResult>(r => r.PricingMethodId)
            .OnDelete(DeleteBehavior.Cascade);

        // Machine Cost Items (MachineryCost method)
        builder.HasMany(m => m.MachineCostItems)
            .WithOne()
            .HasForeignKey(i => i.PricingMethodId)
            .OnDelete(DeleteBehavior.Cascade);

        // Leasehold Analysis (1:1, Leasehold method)
        builder.HasOne(m => m.LeaseholdAnalysis)
            .WithOne()
            .HasForeignKey<LeaseholdAnalysis>(l => l.PricingMethodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ComparativeAnalysisTemplate>()
            .WithMany()
            .HasForeignKey(m => m.ComparativeAnalysisTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.ApproachId);
    }
}

public class PricingComparableLinkConfiguration : IEntityTypeConfiguration<PricingComparableLink>
{
    public void Configure(EntityTypeBuilder<PricingComparableLink> builder)
    {
        builder.ToTable("PricingComparableLinks");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.PricingMethodId).IsRequired();
        builder.Property(c => c.MarketComparableId).IsRequired();
        builder.Property(c => c.DisplaySequence).IsRequired();

        builder.HasIndex(c => new { c.PricingMethodId, c.MarketComparableId }).IsUnique();
    }
}

public class PricingCalculationConfiguration : IEntityTypeConfiguration<PricingCalculation>
{
    public void Configure(EntityTypeBuilder<PricingCalculation> builder)
    {
        builder.ToTable("PricingCalculations");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.PricingMethodId).IsRequired();
        builder.Property(c => c.MarketComparableId).IsRequired();

        builder.Property(c => c.OfferingPrice).HasPrecision(18, 2);
        builder.Property(c => c.OfferingPriceUnit).HasMaxLength(20);
        builder.Property(c => c.AdjustOfferPricePct).HasPrecision(5, 2);
        builder.Property(c => c.AdjustOfferPriceAmt).HasPrecision(18, 2);
        builder.Property(c => c.SellingPrice).HasPrecision(18, 2);
        builder.Property(c => c.SellingPriceUnit).HasMaxLength(20);

        builder.Property(c => c.AdjustedPeriodPct).HasPrecision(5, 2);
        builder.Property(c => c.CumulativeAdjPeriod).HasPrecision(5, 2);

        builder.Property(c => c.LandAreaDeficient).HasPrecision(18, 2);
        builder.Property(c => c.LandAreaDeficientUnit).HasMaxLength(10);
        builder.Property(c => c.LandPrice).HasPrecision(18, 2);
        builder.Property(c => c.LandValueAdjustment).HasPrecision(18, 2);
        builder.Property(c => c.UsableAreaDeficient).HasPrecision(18, 2);
        builder.Property(c => c.UsableAreaDeficientUnit).HasMaxLength(10);
        builder.Property(c => c.UsableAreaPrice).HasPrecision(18, 2);
        builder.Property(c => c.BuildingValueAdjustment).HasPrecision(18, 2);

        builder.Property(c => c.TotalFactorDiffPct).HasPrecision(5, 2);
        builder.Property(c => c.TotalFactorDiffAmt).HasPrecision(18, 2);

        builder.Property(c => c.TotalAdjustedValue).HasPrecision(18, 2);

        builder.Property(c => c.Weight).HasPrecision(10, 5);
        builder.Property(c => c.WeightedAdjustedValue).HasPrecision(18, 2);

        builder.HasIndex(c => c.PricingMethodId);
    }
}

public class PricingFactorScoreConfiguration : IEntityTypeConfiguration<PricingFactorScore>
{
    public void Configure(EntityTypeBuilder<PricingFactorScore> builder)
    {
        builder.ToTable("PricingFactorScores");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(f => f.PricingMethodId).IsRequired();
        builder.Property(f => f.MarketComparableId); // Nullable - null = Collateral
        builder.Property(f => f.FactorId).IsRequired();

        builder.Property(f => f.Value).HasMaxLength(500);
        builder.Property(f => f.Score).HasPrecision(5, 2);
        builder.Property(f => f.FactorWeight).IsRequired().HasPrecision(5, 2);
        builder.Property(f => f.WeightedScore).HasPrecision(5, 2);
        builder.Property(f => f.Intensity).HasPrecision(5, 2);
        builder.Property(f => f.AdjustmentPct).HasPrecision(5, 2);
        builder.Property(f => f.AdjustmentAmt).HasPrecision(18, 2);
        builder.Property(f => f.ComparisonResult).HasMaxLength(20);

        builder.Property(f => f.DisplaySequence).IsRequired();
        builder.Property(f => f.Remarks).HasMaxLength(500);

        builder.HasIndex(f => f.PricingMethodId);
        builder.HasIndex(f => new { f.PricingMethodId, f.MarketComparableId, f.FactorId }).IsUnique();
    }
}

public class PricingComparativeFactorConfiguration : IEntityTypeConfiguration<PricingComparativeFactor>
{
    public void Configure(EntityTypeBuilder<PricingComparativeFactor> builder)
    {
        builder.ToTable("PricingComparativeFactors");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(f => f.PricingMethodId).IsRequired();
        builder.Property(f => f.FactorId).IsRequired();
        builder.Property(f => f.DisplaySequence).IsRequired();
        builder.Property(f => f.IsSelectedForScoring).IsRequired().HasDefaultValue(false);
        builder.Property(f => f.Remarks).HasMaxLength(500);

        builder.HasIndex(f => f.PricingMethodId);
        builder.HasIndex(f => new { f.PricingMethodId, f.FactorId }).IsUnique();
    }
}

public class PricingRsqResultConfiguration : IEntityTypeConfiguration<PricingRsqResult>
{
    public void Configure(EntityTypeBuilder<PricingRsqResult> builder)
    {
        builder.ToTable("PricingRsqResults");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.PricingMethodId).IsRequired();
        builder.HasIndex(r => r.PricingMethodId).IsUnique();

        builder.Property(r => r.CoefficientOfDecision).HasPrecision(18, 10);
        builder.Property(r => r.StandardError).HasPrecision(18, 2);
        builder.Property(r => r.IntersectionPoint).HasPrecision(18, 2);
        builder.Property(r => r.Slope).HasPrecision(18, 2);
        builder.Property(r => r.RsqFinalValue).HasPrecision(18, 2);
        builder.Property(r => r.LowestEstimate).HasPrecision(18, 2);
        builder.Property(r => r.HighestEstimate).HasPrecision(18, 2);
    }
}

public class PricingFinalValueConfiguration : IEntityTypeConfiguration<PricingFinalValue>
{
    public void Configure(EntityTypeBuilder<PricingFinalValue> builder)
    {
        builder.ToTable("PricingFinalValues");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(f => f.PricingMethodId).IsRequired();
        builder.HasIndex(f => f.PricingMethodId).IsUnique();

        builder.Property(f => f.FinalValue).IsRequired().HasPrecision(18, 2);
        builder.Property(f => f.FinalValueRounded).IsRequired().HasPrecision(18, 2);

        builder.Property(f => f.LandArea).HasPrecision(18, 2);
        builder.Property(f => f.AppraisalPrice).HasPrecision(18, 2);
        builder.Property(f => f.AppraisalPriceRounded).HasPrecision(18, 2);
        builder.Property(f => f.PriceDifferentiate).HasPrecision(18, 2);

        builder.Property(f => f.BuildingCost).HasPrecision(18, 2);
        builder.Property(f => f.AppraisalPriceWithBuilding).HasPrecision(18, 2);
        builder.Property(f => f.AppraisalPriceWithBuildingRounded).HasPrecision(18, 2);
    }
}

public class MachineCostItemConfiguration : IEntityTypeConfiguration<MachineCostItem>
{
    public void Configure(EntityTypeBuilder<MachineCostItem> builder)
    {
        builder.ToTable("MachineCostItems");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.PricingMethodId).IsRequired();
        builder.Property(i => i.AppraisalPropertyId).IsRequired();
        builder.Property(i => i.DisplaySequence).IsRequired();

        builder.Property(i => i.RcnReplacementCost).HasPrecision(18, 2);
        builder.Property(i => i.LifeSpanYears).HasPrecision(5, 1);
        builder.Property(i => i.ConditionFactor).HasPrecision(5, 2);
        builder.Property(i => i.FunctionalObsolescence).HasPrecision(5, 2);
        builder.Property(i => i.EconomicObsolescence).HasPrecision(5, 2);
        builder.Property(i => i.FairMarketValue).HasPrecision(18, 2);

        builder.Property(i => i.Notes).HasMaxLength(1000);

        builder.HasIndex(i => i.PricingMethodId);
        builder.HasIndex(i => new { i.PricingMethodId, i.AppraisalPropertyId }).IsUnique();
    }
}

public class LeaseholdAnalysisConfiguration : IEntityTypeConfiguration<LeaseholdAnalysis>
{
    public void Configure(EntityTypeBuilder<LeaseholdAnalysis> builder)
    {
        builder.ToTable("LeaseholdAnalyses");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.PricingMethodId).IsRequired();
        builder.HasIndex(l => l.PricingMethodId).IsUnique();

        // Input fields
        builder.Property(l => l.LandValuePerSqWa).HasPrecision(18, 2);
        builder.Property(l => l.LandGrowthRateType).IsRequired().HasMaxLength(20);
        builder.Property(l => l.LandGrowthRatePercent).HasPrecision(10, 4);
        builder.Property(l => l.LandGrowthIntervalYears);
        builder.Property(l => l.ConstructionCostIndex).HasPrecision(10, 4);
        builder.Property(l => l.InitialBuildingValue).HasPrecision(18, 2);
        builder.Property(l => l.DepreciationRate).HasPrecision(10, 4);
        builder.Property(l => l.DepreciationIntervalYears);
        builder.Property(l => l.BuildingCalcStartYear);
        builder.Property(l => l.DiscountRate).HasPrecision(10, 4);

        // Computed fields
        builder.Property(l => l.TotalIncomeOverLeaseTerm).HasPrecision(18, 2);
        builder.Property(l => l.ValueAtLeaseExpiry).HasPrecision(18, 2);
        builder.Property(l => l.FinalValue).HasPrecision(18, 2);
        builder.Property(l => l.FinalValueRounded).HasPrecision(18, 2);

        // Partial usage fields
        builder.Property(l => l.PartialRai).HasPrecision(18, 2);
        builder.Property(l => l.PartialNgan).HasPrecision(18, 2);
        builder.Property(l => l.PartialWa).HasPrecision(18, 2);
        builder.Property(l => l.PartialLandArea).HasPrecision(18, 2);
        builder.Property(l => l.PricePerSqWa).HasPrecision(18, 2);
        builder.Property(l => l.PartialLandPrice).HasPrecision(18, 2);
        builder.Property(l => l.EstimateNetPrice).HasPrecision(18, 2);
        builder.Property(l => l.EstimatePriceRounded).HasPrecision(18, 2);

        builder.HasMany(l => l.LandGrowthPeriods)
            .WithOne()
            .HasForeignKey(p => p.LeaseholdAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(l => l.TableRows)
            .WithOne()
            .HasForeignKey(r => r.LeaseholdAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LeaseholdLandGrowthPeriodConfiguration : IEntityTypeConfiguration<LeaseholdLandGrowthPeriod>
{
    public void Configure(EntityTypeBuilder<LeaseholdLandGrowthPeriod> builder)
    {
        builder.ToTable("LeaseholdLandGrowthPeriods");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(p => p.LeaseholdAnalysisId).IsRequired();
        builder.Property(p => p.FromYear);
        builder.Property(p => p.ToYear);
        builder.Property(p => p.GrowthRatePercent).HasPrecision(10, 4);

        builder.HasIndex(p => p.LeaseholdAnalysisId);
    }
}

public class LeaseholdCalculationDetailConfiguration : IEntityTypeConfiguration<LeaseholdCalculationDetail>
{
    public void Configure(EntityTypeBuilder<LeaseholdCalculationDetail> builder)
    {
        builder.ToTable("LeaseholdCalculationDetails");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.LeaseholdAnalysisId).IsRequired();
        builder.Property(r => r.DisplaySequence);
        builder.Property(r => r.Year).HasPrecision(10, 2);
        builder.Property(r => r.LandValue).HasPrecision(18, 2);
        builder.Property(r => r.LandGrowthPercent).HasPrecision(10, 4);
        builder.Property(r => r.BuildingValue).HasPrecision(18, 2);
        builder.Property(r => r.DepreciationAmount).HasPrecision(18, 2);
        builder.Property(r => r.DepreciationPercent).HasPrecision(10, 4);
        builder.Property(r => r.BuildingAfterDepreciation).HasPrecision(18, 2);
        builder.Property(r => r.TotalLandAndBuilding).HasPrecision(18, 2);
        builder.Property(r => r.RentalIncome).HasPrecision(18, 2);
        builder.Property(r => r.PvFactor).HasPrecision(18, 10);
        builder.Property(r => r.NetCurrentRentalIncome).HasPrecision(18, 2);

        builder.HasIndex(r => r.LeaseholdAnalysisId);
    }
}