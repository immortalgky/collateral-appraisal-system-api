namespace Appraisal.Infrastructure.Configurations.Projects;

public class ProjectUnitConfiguration : IEntityTypeConfiguration<ProjectUnit>
{
    public void Configure(EntityTypeBuilder<ProjectUnit> builder)
    {
        builder.ToTable("ProjectUnits");

        // Primary Key — no server default
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Foreign Key to Project
        builder.Property(e => e.ProjectId).IsRequired();
        builder.HasIndex(e => e.ProjectId);

        // Upload Batch FK
        builder.Property(e => e.UploadBatchId).IsRequired();
        builder.HasIndex(e => e.UploadBatchId);

        // Composite index for fast per-project sequence lookups
        builder.HasIndex(e => new { e.ProjectId, e.SequenceNumber });

        // Core Properties
        builder.Property(e => e.SequenceNumber).IsRequired();
        builder.Property(e => e.ModelType).HasMaxLength(200);
        builder.Property(e => e.UsableArea).HasPrecision(10, 2);
        builder.Property(e => e.SellingPrice).HasPrecision(18, 2);

        // Condo-side
        builder.Property(e => e.TowerName).HasMaxLength(200);
        builder.Property(e => e.CondoRegistrationNumber).HasMaxLength(100);
        builder.Property(e => e.RoomNumber).HasMaxLength(50);

        // LB-side
        builder.Property(e => e.PlotNumber).HasMaxLength(100);
        builder.Property(e => e.HouseNumber).HasMaxLength(100);
        builder.Property(e => e.LandArea).HasPrecision(10, 2);

        // Optional FK to ProjectTower (Condo path) — NoAction to avoid multiple cascade paths
        builder.HasOne<ProjectTower>()
            .WithMany()
            .HasForeignKey(u => u.ProjectTowerId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        // Optional FK to ProjectModel (both paths) — NoAction
        builder.HasOne<ProjectModel>()
            .WithMany()
            .HasForeignKey(u => u.ProjectModelId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
