using Parameter.PricingParameters.Models;

namespace Parameter.PricingParameters.Configurations;

public class PricingParameterJobPositionConfiguration : IEntityTypeConfiguration<PricingParameterJobPosition>
{
    public void Configure(EntityTypeBuilder<PricingParameterJobPosition> builder)
    {
        builder.ToTable("PricingParameterJobPositions");

        builder.HasKey(j => j.Code);
        builder.Property(j => j.Code).IsRequired().HasMaxLength(10);
        builder.Property(j => j.Name).IsRequired().HasMaxLength(200);
        builder.Property(j => j.DisplaySeq).IsRequired();
    }
}
