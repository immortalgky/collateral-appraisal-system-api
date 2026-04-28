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
    }
}
