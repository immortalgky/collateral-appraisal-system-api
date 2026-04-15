using Parameter.PricingParameters.Models;

namespace Parameter.PricingParameters.Configurations;

public class PricingParameterAssumptionMethodConfiguration : IEntityTypeConfiguration<PricingParameterAssumptionMethod>
{
    public void Configure(EntityTypeBuilder<PricingParameterAssumptionMethod> builder)
    {
        builder.ToTable("PricingParameterAssumptionMethods");

        builder.HasKey(am => new { am.AssumptionType, am.MethodTypeCode });
        builder.Property(am => am.AssumptionType).IsRequired().HasMaxLength(10);
        builder.Property(am => am.MethodTypeCode).IsRequired().HasMaxLength(5);
    }
}
