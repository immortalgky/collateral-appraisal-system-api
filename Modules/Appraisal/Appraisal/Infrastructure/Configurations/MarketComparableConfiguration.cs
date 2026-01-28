namespace Appraisal.Infrastructure.Configurations;

public class MarketComparableConfiguration : IEntityTypeConfiguration<MarketComparable>
{
    public void Configure(EntityTypeBuilder<MarketComparable> builder)
    {
        builder.ToTable("MarketComparables");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(m => m.ComparableNumber).IsRequired().HasMaxLength(50);
        builder.Property(m => m.PropertyType).IsRequired().HasMaxLength(50);

        builder.Property(m => m.Province).IsRequired().HasMaxLength(100);
        builder.Property(m => m.District).HasMaxLength(100);
        builder.Property(m => m.SubDistrict).HasMaxLength(100);
        builder.Property(m => m.Address).HasMaxLength(500);
        builder.Property(m => m.Latitude).HasPrecision(10, 7);
        builder.Property(m => m.Longitude).HasPrecision(10, 7);

        builder.Property(m => m.TransactionType).HasMaxLength(50);
        builder.Property(m => m.TransactionPrice).HasPrecision(18, 2);
        builder.Property(m => m.PricePerUnit).HasPrecision(18, 2);
        builder.Property(m => m.UnitType).HasMaxLength(20);

        builder.Property(m => m.DataSource).IsRequired().HasMaxLength(50);
        builder.Property(m => m.DataConfidence).HasMaxLength(20);
        builder.Property(m => m.Status).IsRequired().HasMaxLength(20);

        builder.Property(m => m.Description).HasMaxLength(1000);
        builder.Property(m => m.Notes).HasMaxLength(2000);

        builder.Property(m => m.CreatedOn).IsRequired();
        builder.Property(m => m.CreatedBy).IsRequired();

        builder.OwnsOne(m => m.SoftDelete, sd =>
        {
            sd.Property(s => s.IsDeleted).HasColumnName("IsDeleted").IsRequired().HasDefaultValue(false);
            sd.Property(s => s.DeletedOn).HasColumnName("DeletedOn");
            sd.Property(s => s.DeletedBy).HasColumnName("DeletedBy");
        });

        builder.Ignore(m => m.DomainEvents);

        // Navigation to factor data (EAV)
        builder.HasMany(m => m.FactorData)
            .WithOne()
            .HasForeignKey(d => d.MarketComparableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(m => m.FactorData).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Navigation to images
        builder.HasMany(m => m.Images)
            .WithOne()
            .HasForeignKey(i => i.MarketComparableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(m => m.Images).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Template reference (optional FK - if template deleted, set to null)
        builder.Property(m => m.TemplateId);
        builder.HasOne<MarketComparableTemplate>()
            .WithMany()
            .HasForeignKey(m => m.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.ComparableNumber).IsUnique();
        builder.HasIndex(m => m.PropertyType);
        builder.HasIndex(m => m.Province);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.TemplateId);
    }
}

public class AppraisalComparableConfiguration : IEntityTypeConfiguration<AppraisalComparable>
{
    public void Configure(EntityTypeBuilder<AppraisalComparable> builder)
    {
        builder.ToTable("AppraisalComparables");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.AppraisalId).IsRequired();
        builder.Property(a => a.MarketComparableId).IsRequired();
        builder.Property(a => a.SequenceNumber).IsRequired();
        builder.Property(a => a.Weight).HasPrecision(5, 2);

        builder.Property(a => a.OriginalPricePerUnit).HasPrecision(18, 2);
        builder.Property(a => a.AdjustedPricePerUnit).HasPrecision(18, 2);
        builder.Property(a => a.TotalAdjustmentPct).HasPrecision(10, 4);
        builder.Property(a => a.WeightedValue).HasPrecision(18, 2);

        builder.Property(a => a.SelectionReason).HasMaxLength(500);
        builder.Property(a => a.Notes).HasMaxLength(1000);

        builder.Property(a => a.CreatedOn).IsRequired();
        builder.Property(a => a.CreatedBy).IsRequired();

        builder.HasMany(a => a.Adjustments)
            .WithOne()
            .HasForeignKey(adj => adj.AppraisalComparableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.Adjustments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(a => a.AppraisalId);
        builder.HasIndex(a => a.MarketComparableId);
        builder.HasIndex(a => new { a.AppraisalId, a.SequenceNumber }).IsUnique();
    }
}

public class ComparableAdjustmentConfiguration : IEntityTypeConfiguration<ComparableAdjustment>
{
    public void Configure(EntityTypeBuilder<ComparableAdjustment> builder)
    {
        builder.ToTable("ComparableAdjustments");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.AppraisalComparableId).IsRequired();
        builder.Property(c => c.AdjustmentCategory).IsRequired().HasMaxLength(50);
        builder.Property(c => c.AdjustmentType).IsRequired().HasMaxLength(100);
        builder.Property(c => c.AdjustmentPercent).HasPrecision(10, 4);
        builder.Property(c => c.AdjustmentDirection).IsRequired().HasMaxLength(20);

        builder.Property(c => c.SubjectValue).HasMaxLength(200);
        builder.Property(c => c.ComparableValue).HasMaxLength(200);
        builder.Property(c => c.Justification).HasMaxLength(500);

        builder.Property(c => c.CreatedOn).IsRequired();
        builder.Property(c => c.CreatedBy).IsRequired();

        builder.HasIndex(c => c.AppraisalComparableId);
    }
}

public class AdjustmentTypeLookupConfiguration : IEntityTypeConfiguration<AdjustmentTypeLookup>
{
    public void Configure(EntityTypeBuilder<AdjustmentTypeLookup> builder)
    {
        builder.ToTable("AdjustmentTypeLookups");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.AdjustmentCategory).IsRequired().HasMaxLength(50);
        builder.Property(a => a.AdjustmentType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Description).IsRequired().HasMaxLength(500);

        builder.Property(a => a.TypicalMinPercent).HasPrecision(10, 4);
        builder.Property(a => a.TypicalMaxPercent).HasPrecision(10, 4);

        builder.Property(a => a.ApplicablePropertyTypes).HasMaxLength(500);

        builder.Property(a => a.CreatedOn).IsRequired();
        builder.Property(a => a.CreatedBy).IsRequired();

        builder.HasIndex(a => a.AdjustmentCategory);
        builder.HasIndex(a => new { a.AdjustmentCategory, a.AdjustmentType }).IsUnique();
    }
}