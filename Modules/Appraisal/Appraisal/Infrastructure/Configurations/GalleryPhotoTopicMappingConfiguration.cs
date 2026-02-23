namespace Appraisal.Infrastructure.Configurations;

public class GalleryPhotoTopicMappingConfiguration : IEntityTypeConfiguration<GalleryPhotoTopicMapping>
{
    public void Configure(EntityTypeBuilder<GalleryPhotoTopicMapping> builder)
    {
        builder.ToTable("GalleryPhotoTopicMappings");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(m => m.GalleryPhotoId).IsRequired();
        builder.Property(m => m.PhotoTopicId).IsRequired();

        builder.HasOne<AppraisalGallery>()
            .WithMany()
            .HasForeignKey(m => m.GalleryPhotoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PhotoTopic>()
            .WithMany()
            .HasForeignKey(m => m.PhotoTopicId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.GalleryPhotoId, m.PhotoTopicId }).IsUnique();
        builder.HasIndex(m => m.PhotoTopicId);
    }
}