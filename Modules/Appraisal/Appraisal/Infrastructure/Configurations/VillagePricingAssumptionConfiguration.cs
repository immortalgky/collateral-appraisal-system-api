namespace Appraisal.Infrastructure.Configurations;

public class VillagePricingAssumptionConfiguration : IEntityTypeConfiguration<VillagePricingAssumption>
{
    public void Configure(EntityTypeBuilder<VillagePricingAssumption> builder)
    {
        builder.ToTable("VillagePricingAssumptions");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign Key (1:1 with Appraisal)
        builder.Property(e => e.AppraisalId).IsRequired();
        builder.HasIndex(e => e.AppraisalId).IsUnique();

        // Location Method
        builder.Property(e => e.LocationMethod).HasMaxLength(200);

        // Adjustments
        builder.Property(e => e.CornerAdjustment).HasPrecision(18, 2);
        builder.Property(e => e.EdgeAdjustment).HasPrecision(18, 2);
        builder.Property(e => e.NearGardenAdjustment).HasPrecision(18, 2);
        builder.Property(e => e.OtherAdjustment).HasPrecision(18, 2);

        // Land Increase/Decrease
        builder.Property(e => e.LandIncreaseDecreaseRate).HasPrecision(18, 2);

        // Force Sale
        builder.Property(e => e.ForceSalePercentage).HasPrecision(5, 2);

        // Model Assumptions (owned collection)
        builder.OwnsMany(e => e.ModelAssumptions, assumption =>
        {
            assumption.ToTable("VillageModelAssumptions");
            assumption.WithOwner().HasForeignKey("VillagePricingAssumptionId");
            assumption.HasKey("Id");
            assumption.Property<Guid>("Id").HasDefaultValueSql("NEWSEQUENTIALID()");

            assumption.Property(a => a.VillageModelId).IsRequired();
            assumption.Property(a => a.ModelType).HasMaxLength(200);
            assumption.Property(a => a.ModelDescription).HasMaxLength(500);
            assumption.Property(a => a.UsableAreaFrom).HasPrecision(18, 2);
            assumption.Property(a => a.UsableAreaTo).HasPrecision(18, 2);
            assumption.Property(a => a.StandardLandPrice).HasPrecision(18, 2);
            assumption.Property(a => a.StandardPrice).HasPrecision(18, 2);
            assumption.Property(a => a.CoverageAmount).HasPrecision(18, 2);
            assumption.Property(a => a.FireInsuranceCondition).HasMaxLength(200);

            assumption.HasIndex("VillagePricingAssumptionId");
        });

        // Backing field for owned collection
        builder.Navigation(e => e.ModelAssumptions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
