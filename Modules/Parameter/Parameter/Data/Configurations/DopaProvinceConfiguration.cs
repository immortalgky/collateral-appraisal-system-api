using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parameter.Addresses.Models;

namespace Parameter.Data.Configurations;

public class DopaProvinceConfiguration : IEntityTypeConfiguration<DopaProvince>
{
    public void Configure(EntityTypeBuilder<DopaProvince> builder)
    {
        builder.ToTable("DopaProvinces");
        builder.HasKey(p => p.Code);
        builder.Property(p => p.Code).HasMaxLength(2).IsRequired();
        builder.Property(p => p.NameTh).HasMaxLength(150).IsRequired();
        builder.Property(p => p.NameEn).HasMaxLength(150);
    }
}
