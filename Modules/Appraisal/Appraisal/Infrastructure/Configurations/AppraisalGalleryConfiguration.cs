namespace Appraisal.Infrastructure.Configurations;

public class AppraisalGalleryConfiguration : IEntityTypeConfiguration<AppraisalGallery>
{
    public void Configure(EntityTypeBuilder<AppraisalGallery> builder)
    {
        builder.ToTable("AppraisalGallery");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(g => g.AppraisalId).IsRequired();
        builder.Property(g => g.DocumentId).IsRequired();
        builder.Property(g => g.PhotoNumber).IsRequired();
        builder.Property(g => g.PhotoType).IsRequired().HasMaxLength(50);
        builder.Property(g => g.PhotoCategory).HasMaxLength(100);
        builder.Property(g => g.Caption).HasMaxLength(500);

        builder.Property(g => g.Latitude).HasPrecision(10, 7);
        builder.Property(g => g.Longitude).HasPrecision(10, 7);

        builder.Property(g => g.UploadedAt).IsRequired();
        builder.Property(g => g.UploadedBy).IsRequired();

        builder.Property(g => g.ReportSection).HasMaxLength(100);

        builder.HasIndex(g => g.AppraisalId);
        builder.HasIndex(g => g.DocumentId);
        builder.HasIndex(g => new { g.AppraisalId, g.PhotoNumber }).IsUnique();
    }
}

public class PropertyPhotoMappingConfiguration : IEntityTypeConfiguration<PropertyPhotoMapping>
{
    public void Configure(EntityTypeBuilder<PropertyPhotoMapping> builder)
    {
        builder.ToTable("PropertyPhotoMappings");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(p => p.GalleryPhotoId).IsRequired();
        builder.Property(p => p.PropertyDetailType).IsRequired().HasMaxLength(50);
        builder.Property(p => p.PropertyDetailId).IsRequired();
        builder.Property(p => p.PhotoPurpose).IsRequired().HasMaxLength(100);
        builder.Property(p => p.SectionReference).HasMaxLength(100);

        builder.Property(p => p.LinkedBy).IsRequired();
        builder.Property(p => p.LinkedAt).IsRequired();

        builder.HasIndex(p => p.GalleryPhotoId);
        builder.HasIndex(p => new { p.PropertyDetailType, p.PropertyDetailId });
    }
}