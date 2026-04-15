using Parameter.PricingParameters.Models;

namespace Parameter.PricingParameters.Configurations;

public class PricingParameterAssumptionTypeConfiguration : IEntityTypeConfiguration<PricingParameterAssumptionType>
{
    public void Configure(EntityTypeBuilder<PricingParameterAssumptionType> builder)
    {
        builder.ToTable("PricingParameterAssumptionTypes");

        builder.HasKey(a => a.Code);
        builder.Property(a => a.Code).IsRequired().HasMaxLength(10);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Category).IsRequired().HasMaxLength(50);
        builder.Property(a => a.DisplaySeq).IsRequired();
    }
}
