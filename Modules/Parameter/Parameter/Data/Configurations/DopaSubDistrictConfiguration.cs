using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parameter.Addresses.Models;

namespace Parameter.Data.Configurations;

public class DopaSubDistrictConfiguration : IEntityTypeConfiguration<DopaSubDistrict>
{
    public void Configure(EntityTypeBuilder<DopaSubDistrict> builder)
    {
        builder.ToTable("DopaSubDistricts");
        builder.HasKey(s => s.Code);
        builder.Property(s => s.Code).HasMaxLength(6).IsRequired();
        builder.Property(s => s.NameTh).HasMaxLength(150).IsRequired();
        builder.Property(s => s.NameEn).HasMaxLength(150);
        builder.Property(s => s.DistrictCode).HasMaxLength(4).IsRequired();
        builder.Property(s => s.Postcode).HasMaxLength(5);

        builder.HasOne(s => s.District)
            .WithMany(d => d.SubDistricts)
            .HasForeignKey(s => s.DistrictCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.DistrictCode)
            .HasDatabaseName("IX_DopaSubDistricts_DistrictCode");
    }
}
