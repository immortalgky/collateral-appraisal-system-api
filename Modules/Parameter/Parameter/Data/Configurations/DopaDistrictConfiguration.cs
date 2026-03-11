using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parameter.Addresses.Models;

namespace Parameter.Data.Configurations;

public class DopaDistrictConfiguration : IEntityTypeConfiguration<DopaDistrict>
{
    public void Configure(EntityTypeBuilder<DopaDistrict> builder)
    {
        builder.ToTable("DopaDistricts");
        builder.HasKey(d => d.Code);
        builder.Property(d => d.Code).HasMaxLength(4).IsRequired();
        builder.Property(d => d.NameTh).HasMaxLength(150).IsRequired();
        builder.Property(d => d.NameEn).HasMaxLength(150);
        builder.Property(d => d.ProvinceCode).HasMaxLength(2).IsRequired();

        builder.HasOne(d => d.Province)
            .WithMany(p => p.Districts)
            .HasForeignKey(d => d.ProvinceCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.ProvinceCode)
            .HasDatabaseName("IX_DopaDistricts_ProvinceCode");
    }
}
