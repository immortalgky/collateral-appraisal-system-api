using Parameter.PricingParameters.Models;

namespace Parameter.PricingParameters.Configurations;

public class PricingParameterFireInsuranceRateConfiguration : IEntityTypeConfiguration<PricingParameterFireInsuranceRate>
{
    public void Configure(EntityTypeBuilder<PricingParameterFireInsuranceRate> builder)
    {
        builder.ToTable("PricingParameterFireInsuranceRates");

        builder.HasKey(r => r.Code);
        builder.Property(r => r.Code).IsRequired().HasMaxLength(10).ValueGeneratedNever();
        builder.Property(r => r.Condition).IsRequired().HasMaxLength(200);
        builder.HasIndex(r => r.Condition).IsUnique();
        builder.Property(r => r.PropertyKind).IsRequired().HasMaxLength(50);
        builder.Property(r => r.RatePerSqm).IsRequired().HasPrecision(18, 2);
        builder.Property(r => r.DisplaySeq).IsRequired();
    }
}
