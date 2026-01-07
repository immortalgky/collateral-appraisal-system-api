namespace Appraisal.Infrastructure.Configurations;

public class ValuationAnalysisConfiguration : IEntityTypeConfiguration<ValuationAnalysis>
{
    public void Configure(EntityTypeBuilder<ValuationAnalysis> builder)
    {
        builder.ToTable("ValuationAnalyses");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(v => v.AppraisalId).IsRequired();
        builder.Property(v => v.ValuationApproach).IsRequired().HasMaxLength(50);
        builder.Property(v => v.ValuationDate).IsRequired();

        builder.Property(v => v.MarketValue).HasPrecision(18, 2);
        builder.Property(v => v.AppraisedValue).HasPrecision(18, 2);
        builder.Property(v => v.ForcedSaleValue).HasPrecision(18, 2);
        builder.Property(v => v.InsuranceValue).HasPrecision(18, 2);
        builder.Property(v => v.Currency).HasMaxLength(3);

        builder.Property(v => v.AppraiserOpinion).HasMaxLength(2000);
        builder.Property(v => v.ValuationNotes).HasMaxLength(2000);

        builder.Property(v => v.CreatedOn).IsRequired();
        builder.Property(v => v.CreatedBy).IsRequired();

        builder.HasMany(v => v.GroupValuations)
            .WithOne()
            .HasForeignKey(g => g.ValuationAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.PropertyValuations)
            .WithOne()
            .HasForeignKey(p => p.ValuationAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(v => v.GroupValuations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(v => v.PropertyValuations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(v => v.AppraisalId).IsUnique();
    }
}

public class GroupValuationConfiguration : IEntityTypeConfiguration<GroupValuation>
{
    public void Configure(EntityTypeBuilder<GroupValuation> builder)
    {
        builder.ToTable("GroupValuations");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(g => g.ValuationAnalysisId).IsRequired();
        builder.Property(g => g.PropertyGroupId).IsRequired();

        builder.Property(g => g.MarketValue).HasPrecision(18, 2);
        builder.Property(g => g.AppraisedValue).HasPrecision(18, 2);
        builder.Property(g => g.ForcedSaleValue).HasPrecision(18, 2);
        builder.Property(g => g.ValuePerUnit).HasPrecision(18, 2);
        builder.Property(g => g.UnitType).HasMaxLength(20);
        builder.Property(g => g.ValuationWeight).HasPrecision(5, 2);
        builder.Property(g => g.ValuationNotes).HasMaxLength(1000);

        builder.Property(g => g.CreatedOn).IsRequired();
        builder.Property(g => g.CreatedBy).IsRequired();

        builder.HasIndex(g => g.ValuationAnalysisId);
        builder.HasIndex(g => g.PropertyGroupId);
    }
}

public class PropertyValuationConfiguration : IEntityTypeConfiguration<PropertyValuation>
{
    public void Configure(EntityTypeBuilder<PropertyValuation> builder)
    {
        builder.ToTable("PropertyValuations");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(p => p.ValuationAnalysisId).IsRequired();
        builder.Property(p => p.PropertyDetailType).IsRequired().HasMaxLength(50);
        builder.Property(p => p.PropertyDetailId).IsRequired();

        builder.Property(p => p.MarketValue).HasPrecision(18, 2);
        builder.Property(p => p.AppraisedValue).HasPrecision(18, 2);
        builder.Property(p => p.ForcedSaleValue).HasPrecision(18, 2);
        builder.Property(p => p.ValuePerUnit).HasPrecision(18, 2);
        builder.Property(p => p.UnitType).HasMaxLength(20);
        builder.Property(p => p.ValuationWeight).HasPrecision(5, 2);
        builder.Property(p => p.ValuationNotes).HasMaxLength(1000);

        builder.Property(p => p.CreatedOn).IsRequired();
        builder.Property(p => p.CreatedBy).IsRequired();

        builder.HasIndex(p => p.ValuationAnalysisId);
        builder.HasIndex(p => new { p.PropertyDetailType, p.PropertyDetailId });
    }
}