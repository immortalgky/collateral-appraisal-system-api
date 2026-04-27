namespace Appraisal.Infrastructure.Configurations.Projects;

public class ProjectPricingAssumptionConfiguration : IEntityTypeConfiguration<ProjectPricingAssumption>
{
    public void Configure(EntityTypeBuilder<ProjectPricingAssumption> builder)
    {
        builder.ToTable("ProjectPricingAssumptions");

        // Primary Key — no server default
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Foreign Key (1:1 with Project)
        builder.Property(e => e.ProjectId).IsRequired();
        builder.HasIndex(e => e.ProjectId).IsUnique();

        // Location Method
        builder.Property(e => e.LocationMethod).HasMaxLength(200);

        // Shared Adjustments
        builder.Property(e => e.CornerAdjustment).HasPrecision(18, 2);
        builder.Property(e => e.EdgeAdjustment).HasPrecision(18, 2);
        builder.Property(e => e.OtherAdjustment).HasPrecision(18, 2);
        builder.Property(e => e.ForceSalePercentage).HasPrecision(5, 2);

        // Condo-only
        builder.Property(e => e.PoolViewAdjustment).HasPrecision(18, 2);
        builder.Property(e => e.SouthAdjustment).HasPrecision(18, 2);
        builder.Property(e => e.FloorIncrementAmount).HasPrecision(18, 2);

        // LB-only
        builder.Property(e => e.NearGardenAdjustment).HasPrecision(18, 2);
        builder.Property(e => e.LandIncreaseDecreaseRate).HasPrecision(18, 2);

        // Model Assumptions (owned collection)
        builder.OwnsMany(e => e.ModelAssumptions, assumption =>
        {
            assumption.ToTable("ProjectModelAssumptions");
            assumption.WithOwner().HasForeignKey("ProjectPricingAssumptionId");
            assumption.HasKey(a => a.Id);
            // Id is set via Guid.CreateVersion7() in the domain factory — no server default needed
            assumption.Property(a => a.Id).ValueGeneratedNever();

            assumption.Property(a => a.ProjectModelId).IsRequired();
            assumption.Property(a => a.ModelType).HasMaxLength(200);
            assumption.Property(a => a.ModelDescription).HasMaxLength(500);
            assumption.Property(a => a.UsableAreaFrom).HasPrecision(18, 2);
            assumption.Property(a => a.UsableAreaTo).HasPrecision(18, 2);
            assumption.Property(a => a.StandardPrice).HasPrecision(18, 2);
            assumption.Property(a => a.StandardLandPrice).HasPrecision(18, 2);
            assumption.Property(a => a.CoverageAmount).HasPrecision(18, 2);
            assumption.Property(a => a.FireInsuranceCondition).HasMaxLength(200);

            assumption.HasIndex("ProjectPricingAssumptionId");
        });

        // Backing field for owned collection
        builder.Navigation(e => e.ModelAssumptions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
