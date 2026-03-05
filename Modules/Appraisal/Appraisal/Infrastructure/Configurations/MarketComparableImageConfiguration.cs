namespace Appraisal.Infrastructure.Configurations;

public class MarketComparableImageConfiguration : IEntityTypeConfiguration<MarketComparableImage>
{
    public void Configure(EntityTypeBuilder<MarketComparableImage> builder)
    {
        builder.ToTable("MarketComparableImages");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.MarketComparableId).IsRequired();
        builder.Property(i => i.GalleryPhotoId).IsRequired();
        builder.Property(i => i.DisplaySequence).IsRequired();
        builder.Property(i => i.Title).HasMaxLength(200);
        builder.Property(i => i.Description).HasMaxLength(500);

        // Only CreatedOn/CreatedBy per spec
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.CreatedBy).IsRequired();
        builder.Ignore(i => i.UpdatedAt);
        builder.Ignore(i => i.UpdatedBy);

        builder.HasIndex(i => i.MarketComparableId);
        builder.HasIndex(i => i.GalleryPhotoId);
        builder.HasIndex(i => new { i.MarketComparableId, i.DisplaySequence });
    }
}