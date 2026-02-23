namespace Appraisal.Infrastructure.Configurations;

// LandTitleConfiguration removed - now configured via OwnsMany in LandAppraisalDetailConfiguration

// BuildingDepreciationDetailConfiguration removed - now configured via OwnsMany in BuildingAppraisalDetailConfiguration

// BuildingAppraisalSurfaceConfiguration removed - now configured via OwnsMany in BuildingAppraisalDetailConfiguration

public class CondoAppraisalAreaDetailConfiguration : IEntityTypeConfiguration<CondoAppraisalAreaDetail>
{
    public void Configure(EntityTypeBuilder<CondoAppraisalAreaDetail> builder)
    {
        builder.ToTable("CondoAppraisalAreaDetails");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.AppraisalPropertyId).IsRequired();
        builder.Property(a => a.AreaDescription).IsRequired().HasMaxLength(200);
        builder.Property(a => a.AreaSize).IsRequired().HasPrecision(10, 2);

        builder.HasIndex(a => a.AppraisalPropertyId);
    }
}

public class LawAndRegulationConfiguration : IEntityTypeConfiguration<LawAndRegulation>
{
    public void Configure(EntityTypeBuilder<LawAndRegulation> builder)
    {
        builder.ToTable("LawAndRegulations");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.AppraisalId).IsRequired();
        builder.Property(l => l.HeaderCode).IsRequired().HasMaxLength(50);

        builder.HasMany(l => l.Images)
            .WithOne()
            .HasForeignKey(i => i.LawAndRegulationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.AppraisalId);
    }
}

public class LawAndRegulationImageConfiguration : IEntityTypeConfiguration<LawAndRegulationImage>
{
    public void Configure(EntityTypeBuilder<LawAndRegulationImage> builder)
    {
        builder.ToTable("LawAndRegulationImages");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.LawAndRegulationId).IsRequired();
        builder.Property(i => i.DocumentId).IsRequired();
        builder.Property(i => i.DisplaySequence).IsRequired();
        builder.Property(i => i.Title).HasMaxLength(200);
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.FileName).IsRequired().HasMaxLength(255);
        builder.Property(i => i.FilePath).IsRequired().HasMaxLength(500);

        builder.HasIndex(i => i.LawAndRegulationId);
    }
}