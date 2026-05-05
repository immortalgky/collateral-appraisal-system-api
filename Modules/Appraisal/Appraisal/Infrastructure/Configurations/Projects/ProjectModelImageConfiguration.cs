using Appraisal.Domain.Projects;

namespace Appraisal.Infrastructure.Configurations.Projects;

public class ProjectModelImageConfiguration : IEntityTypeConfiguration<ProjectModelImage>
{
    public void Configure(EntityTypeBuilder<ProjectModelImage> builder)
    {
        builder.ToTable("ProjectModelImages");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.ProjectModelId).IsRequired();
        builder.Property(i => i.GalleryPhotoId).IsRequired();
        builder.Property(i => i.DisplaySequence).IsRequired();
        builder.Property(i => i.Title).HasMaxLength(200);
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.IsThumbnail).IsRequired().HasDefaultValue(false);

        // Only CreatedAt/CreatedBy tracked; UpdatedAt/UpdatedBy are intentionally excluded.
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.CreatedBy).IsRequired();
        builder.Ignore(i => i.UpdatedAt);
        builder.Ignore(i => i.UpdatedBy);

        // Composite index covers lookups by ProjectModelId; single-column index is redundant.
        builder.HasIndex(i => i.GalleryPhotoId);
        builder.HasIndex(i => new { i.ProjectModelId, i.DisplaySequence });
        // Note: no filtered unique index on IsThumbnail — SQL Server enforces it
        // statement-by-statement, which causes a constraint violation when EF batches
        // SET-true before SET-false. The aggregate's SetThumbnail() enforces the invariant.
    }
}
