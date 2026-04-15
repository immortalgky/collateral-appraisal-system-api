using Parameter.PricingParameters.Models;

namespace Parameter.PricingParameters.Configurations;

public class PricingParameterTaxBracketConfiguration : IEntityTypeConfiguration<PricingParameterTaxBracket>
{
    public void Configure(EntityTypeBuilder<PricingParameterTaxBracket> builder)
    {
        builder.ToTable("PricingParameterTaxBrackets");

        builder.HasKey(t => t.Tier);
        builder.Property(t => t.Tier).IsRequired().ValueGeneratedNever();
        builder.Property(t => t.TaxRate).IsRequired().HasPrecision(5, 4);
        builder.Property(t => t.MinValue).IsRequired().HasPrecision(18, 2);
        builder.Property(t => t.MaxValue).HasPrecision(18, 2);
    }
}
