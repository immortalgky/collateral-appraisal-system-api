namespace Appraisal.Infrastructure.Configurations.Projects;

public class ProjectUnitUploadConfiguration : IEntityTypeConfiguration<ProjectUnitUpload>
{
    public void Configure(EntityTypeBuilder<ProjectUnitUpload> builder)
    {
        builder.ToTable("ProjectUnitUploads");

        // Primary Key — no server default
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Foreign Key to Project
        builder.Property(e => e.ProjectId).IsRequired();
        builder.HasIndex(e => e.ProjectId);

        // Core Properties
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.UploadedAt).IsRequired();
        builder.Property(e => e.IsUsed).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.IsSystemGenerated).IsRequired().HasDefaultValue(false);

        // Outcome counters. AddedUnits applies to every path so it is required; the rest are
        // re-match only and stay NULL, which the UI reads as "this kind of batch does not report it".
        builder.Property(e => e.AddedUnits).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.MatchedUnsoldUnits);
        builder.Property(e => e.AutoSoldUnits);
        builder.Property(e => e.UpdatedUnits);
    }
}
