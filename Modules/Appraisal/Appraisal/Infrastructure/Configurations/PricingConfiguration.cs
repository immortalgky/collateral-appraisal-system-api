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
        builder.Property(f => f.AdjustmentPct).HasPrecision(5, 2);

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