namespace Appraisal.Infrastructure.Configurations;

public class LandTitleConfiguration : IEntityTypeConfiguration<LandTitle>
{
    public void Configure(EntityTypeBuilder<LandTitle> builder)
    {
        builder.ToTable("LandTitles");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.AppraisalPropertyId).IsRequired();
        builder.Property(t => t.SequenceNumber).IsRequired();

        builder.Property(t => t.TitleDeedNumber).IsRequired().HasMaxLength(100);
        builder.Property(t => t.BookNumber).HasMaxLength(50);
        builder.Property(t => t.PageNumber).HasMaxLength(50);
        builder.Property(t => t.LandNumber).HasMaxLength(50);
        builder.Property(t => t.SurveyNumber).HasMaxLength(50);
        builder.Property(t => t.SheetNumber).HasMaxLength(50);

        builder.Property(t => t.DocumentType).IsRequired().HasMaxLength(50);
        builder.Property(t => t.Rawang).HasMaxLength(100);
        builder.Property(t => t.AerialPhotoNumber).HasMaxLength(100);
        builder.Property(t => t.AerialPhotoName).HasMaxLength(200);

        builder.Property(t => t.AreaSquareWa).HasPrecision(10, 2);
        builder.Property(t => t.TotalAreaInSquareWa).HasPrecision(18, 2);

        builder.Property(t => t.BoundaryMarker).HasMaxLength(50);
        builder.Property(t => t.BoundaryMarkerOther).HasMaxLength(200);
        builder.Property(t => t.DocumentValidation).HasMaxLength(50);

        builder.Property(t => t.PricePerSquareWa).HasPrecision(18, 2);
        builder.Property(t => t.GovernmentPrice).HasPrecision(18, 2);

        builder.HasIndex(t => t.AppraisalPropertyId);
        builder.HasIndex(t => new { t.AppraisalPropertyId, t.SequenceNumber }).IsUnique();
    }
}

public class BuildingDepreciationDetailConfiguration : IEntityTypeConfiguration<BuildingDepreciationDetail>
{
    public void Configure(EntityTypeBuilder<BuildingDepreciationDetail> builder)
    {
        builder.ToTable("BuildingDepreciationDetails");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(d => d.AppraisalPropertyId).IsRequired();
        builder.Property(d => d.DepreciationMethod).IsRequired().HasMaxLength(50);
        builder.Property(d => d.UsefulLifeYears).IsRequired();
        builder.Property(d => d.EffectiveAge).IsRequired();
        builder.Property(d => d.RemainingLifeYears).IsRequired();
        builder.Property(d => d.SalvageValuePercent).HasPrecision(5, 2);

        builder.Property(d => d.ReplacementCostNew).IsRequired().HasPrecision(18, 2);
        builder.Property(d => d.PhysicalDepreciationPct).IsRequired().HasPrecision(5, 2);
        builder.Property(d => d.PhysicalDepreciationAmt).IsRequired().HasPrecision(18, 2);
        builder.Property(d => d.FunctionalObsolescencePct).HasPrecision(5, 2);
        builder.Property(d => d.FunctionalObsolescenceAmt).HasPrecision(18, 2);
        builder.Property(d => d.ExternalObsolescencePct).HasPrecision(5, 2);
        builder.Property(d => d.ExternalObsolescenceAmt).HasPrecision(18, 2);
        builder.Property(d => d.TotalDepreciationPct).IsRequired().HasPrecision(5, 2);
        builder.Property(d => d.TotalDepreciationAmt).IsRequired().HasPrecision(18, 2);
        builder.Property(d => d.DepreciatedValue).IsRequired().HasPrecision(18, 2);

        builder.Property(d => d.StructuralCondition).HasMaxLength(50);
        builder.Property(d => d.MaintenanceLevel).HasMaxLength(50);

        builder.HasIndex(d => d.AppraisalPropertyId);
    }
}

public class BuildingAppraisalSurfaceConfiguration : IEntityTypeConfiguration<BuildingAppraisalSurface>
{
    public void Configure(EntityTypeBuilder<BuildingAppraisalSurface> builder)
    {
        builder.ToTable("BuildingAppraisalSurfaces");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.AppraisalPropertyId).IsRequired();
        builder.Property(s => s.FromFloorNo).IsRequired();
        builder.Property(s => s.ToFloorNo).IsRequired();

        builder.Property(s => s.FloorType).HasMaxLength(50);
        builder.Property(s => s.FloorStructure).HasMaxLength(50);
        builder.Property(s => s.FloorStructureOther).HasMaxLength(200);
        builder.Property(s => s.FloorSurface).HasMaxLength(50);
        builder.Property(s => s.FloorSurfaceOther).HasMaxLength(200);

        builder.HasIndex(s => s.AppraisalPropertyId);
    }
}

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
        builder.Property(i => i.DisplaySequence).IsRequired();
        builder.Property(i => i.Title).HasMaxLength(200);
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.FileName).IsRequired().HasMaxLength(255);
        builder.Property(i => i.FilePath).IsRequired().HasMaxLength(500);

        builder.HasIndex(i => i.LawAndRegulationId);
    }
}