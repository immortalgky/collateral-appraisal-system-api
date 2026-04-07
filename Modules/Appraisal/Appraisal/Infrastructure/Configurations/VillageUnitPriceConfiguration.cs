namespace Appraisal.Infrastructure.Configurations;

public class VillageUnitPriceConfiguration : IEntityTypeConfiguration<VillageUnitPrice>
{
    public void Configure(EntityTypeBuilder<VillageUnitPrice> builder)
    {
        builder.ToTable("VillageUnitPrices");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign Key (1:1 with VillageUnit) — cascade delete when unit is removed
        builder.Property(e => e.VillageUnitId).IsRequired();
        builder.HasIndex(e => e.VillageUnitId).IsUnique();
        builder.HasOne<VillageUnit>()
            .WithOne()
            .HasForeignKey<VillageUnitPrice>(e => e.VillageUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        // Calculated Values
        builder.Property(e => e.LandIncreaseDecreaseAmount).HasPrecision(18, 2);
        builder.Property(e => e.AdjustPriceLocation).HasPrecision(18, 2);
        builder.Property(e => e.StandardPrice).HasPrecision(18, 2);
        builder.Property(e => e.TotalAppraisalValue).HasPrecision(18, 2);
        builder.Property(e => e.TotalAppraisalValueRounded).HasPrecision(18, 2);
        builder.Property(e => e.ForceSellingPrice).HasPrecision(18, 2);
        builder.Property(e => e.CoverageAmount).HasPrecision(18, 2);
    }
}
