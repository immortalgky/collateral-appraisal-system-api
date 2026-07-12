using Parameter.Dealers.Models;

namespace Parameter.Dealers.Configurations;

public class DealerConfiguration : IEntityTypeConfiguration<Dealer>
{
    public void Configure(EntityTypeBuilder<Dealer> builder)
    {
        builder.ToTable("Dealers");

        builder.HasKey(d => d.DealerCode);
        builder.Property(d => d.DealerCode).IsRequired().HasMaxLength(20);
        builder.Property(d => d.DealerName).IsRequired().HasMaxLength(200);
    }
}
