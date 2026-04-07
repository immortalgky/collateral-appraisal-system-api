namespace Appraisal.Infrastructure.Configurations;

public class CondoUnitPriceConfiguration : IEntityTypeConfiguration<CondoUnitPrice>
{
    public void Configure(EntityTypeBuilder<CondoUnitPrice> builder)
    {
        builder.ToTable("CondoUnitPrices");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign Key (1:1 with CondoUnit) — cascade delete when unit is removed
        builder.Property(e => e.CondoUnitId).IsRequired();
        builder.HasIndex(e => e.CondoUnitId).IsUnique();
        builder.HasOne<CondoUnit>()
            .WithOne()
            .HasForeignKey<CondoUnitPrice>(e => e.CondoUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        // Calculated Values
        builder.Property(e => e.AdjustPriceLocation).HasPrecision(18, 2);
        builder.Property(e => e.StandardPrice).HasPrecision(18, 2);
        builder.Property(e => e.PriceIncrementPerFloor).HasPrecision(18, 2);
        builder.Property(e => e.TotalAppraisalValue).HasPrecision(18, 2);
        builder.Property(e => e.TotalAppraisalValueRounded).HasPrecision(18, 2);
        builder.Property(e => e.ForceSellingPrice).HasPrecision(18, 2);
        builder.Property(e => e.CoverageAmount).HasPrecision(18, 2);
    }
}
