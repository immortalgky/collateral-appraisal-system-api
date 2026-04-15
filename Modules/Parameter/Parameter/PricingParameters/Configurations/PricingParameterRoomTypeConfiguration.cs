using Parameter.PricingParameters.Models;

namespace Parameter.PricingParameters.Configurations;

public class PricingParameterRoomTypeConfiguration : IEntityTypeConfiguration<PricingParameterRoomType>
{
    public void Configure(EntityTypeBuilder<PricingParameterRoomType> builder)
    {
        builder.ToTable("PricingParameterRoomTypes");

        builder.HasKey(r => r.Code);
        builder.Property(r => r.Code).IsRequired().HasMaxLength(10);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.DisplaySeq).IsRequired();
    }
}
