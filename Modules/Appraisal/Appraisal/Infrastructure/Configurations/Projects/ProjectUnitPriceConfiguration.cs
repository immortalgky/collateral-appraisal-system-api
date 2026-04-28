namespace Appraisal.Infrastructure.Configurations.Projects;

public class ProjectUnitPriceConfiguration : IEntityTypeConfiguration<ProjectUnitPrice>
{
    public void Configure(EntityTypeBuilder<ProjectUnitPrice> builder)
    {
        builder.ToTable("ProjectUnitPrices");

        // Primary Key — no server default
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // FK to ProjectUnit (1:1) — cascade delete so removing a unit also removes its price row
        builder.Property(e => e.ProjectUnitId).IsRequired();
        builder.HasIndex(e => e.ProjectUnitId).IsUnique();
        builder.HasOne<ProjectUnit>()
            .WithOne()
            .HasForeignKey<ProjectUnitPrice>(e => e.ProjectUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        // Location Flags (bool defaults to false)
        builder.Property(e => e.IsCorner).HasDefaultValue(false);
        builder.Property(e => e.IsEdge).HasDefaultValue(false);
        builder.Property(e => e.IsOther).HasDefaultValue(false);
        builder.Property(e => e.IsPoolView).HasDefaultValue(false);
        builder.Property(e => e.IsSouth).HasDefaultValue(false);
        builder.Property(e => e.IsNearGarden).HasDefaultValue(false);

        // Calculated Values
        builder.Property(e => e.LandIncreaseDecreaseAmount).HasPrecision(18, 2);
        builder.Property(e => e.AdjustPriceLocation).HasPrecision(18, 2);
        builder.Property(e => e.StandardPrice).HasPrecision(18, 2);
        builder.Property(e => e.PriceIncrementPerFloor).HasPrecision(18, 2);
        builder.Property(e => e.TotalAppraisalValue).HasPrecision(18, 2);
        builder.Property(e => e.TotalAppraisalValueRounded).HasPrecision(18, 2);
        builder.Property(e => e.ForceSellingPrice).HasPrecision(18, 2);
        builder.Property(e => e.CoverageAmount).HasPrecision(18, 2);
    }
}
