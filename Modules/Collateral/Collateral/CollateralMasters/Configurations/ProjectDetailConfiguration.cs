using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class ProjectDetailConfiguration : IEntityTypeConfiguration<ProjectDetail>
{
    public void Configure(EntityTypeBuilder<ProjectDetail> builder)
    {
        builder.ToTable("ProjectDetails");

        builder.HasKey(d => d.CollateralMasterId);

        builder.Property(d => d.ProjectType).IsRequired().HasMaxLength(50);
        // ← appraisal.Projects.ProjectName nvarchar(500)
        builder.Property(d => d.ProjectName).HasMaxLength(500);
        builder.Property(d => d.Developer).HasMaxLength(300);
        builder.Property(d => d.Address).HasMaxLength(500);
        builder.Property(d => d.Province).HasMaxLength(100);
        // Stays (9,6) although appraisal stores (10,7). ProjectDetails has no GeoPoint column, but the
        // three collateral tables are kept on one precision rather than leaving this one an outlier.
        // See LandDetailConfiguration for the full reasoning.
        builder.Property(d => d.Latitude).HasPrecision(9, 6);
        builder.Property(d => d.Longitude).HasPrecision(9, 6);
        builder.Property(d => d.TotalUnits).IsRequired();
        builder.Property(d => d.RemainingUnits).IsRequired();
        builder.Property(d => d.ProjectSellingPrice).HasPrecision(18, 2);
        // StructureJson column removed in Phase 1 — replaced by collateral.ProjectUnits table.

        // AppraisalSummary (owned — flat columns, same pattern as CondoDetail)

        builder.Property(d => d.IsDeleted).IsRequired().HasDefaultValue(false);

        // Navigation: Units (1:N via CollateralMasterId FK on ProjectUnit)
        builder.HasMany(d => d.Units)
            .WithOne()
            .HasForeignKey(u => u.CollateralMasterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ProjectUnits_CollateralMasters");
        builder.Navigation(d => d.Units).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
