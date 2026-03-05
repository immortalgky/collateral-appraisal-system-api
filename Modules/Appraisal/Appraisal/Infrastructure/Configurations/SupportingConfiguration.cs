namespace Appraisal.Infrastructure.Configurations;

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
        builder.Property(i => i.GalleryPhotoId).IsRequired();
        builder.Property(i => i.DisplaySequence).IsRequired();
        builder.Property(i => i.Title).HasMaxLength(200);
        builder.Property(i => i.Description).HasMaxLength(500);

        builder.HasIndex(i => i.LawAndRegulationId);
        builder.HasIndex(i => i.GalleryPhotoId);
    }
}