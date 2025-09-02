using Collateral.CollateralProperties.Models;

namespace Collateral.Data.Configurations;

public class CollateralLandConfiguration : IEntityTypeConfiguration<CollateralLand>
{
    public void Configure(EntityTypeBuilder<CollateralLand> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).UseIdentityColumn().HasColumnName("LandId");

        builder.OwnsOne(
            p => p.Coordinate,
            coordinate =>
            {
                coordinate.Property(p => p.Latitude).UseLatLonConfig().HasColumnName("Latitude");

                coordinate.Property(p => p.Longitude).UseLatLonConfig().HasColumnName("Longitude");
            }
        );

        builder.OwnsOne(
            p => p.CollateralLocation,
            location =>
            {
                location
                    .Property(p => p.SubDistrict)
                    .UseSubDistrictConfig()
                    .HasColumnName("SubDistrict");

                location
                    .Property(p => p.District)
                    .UseDistrictConfig()
                    .HasColumnName("District");

                location
                    .Property(p => p.Province)
                    .UseProvinceConfig()
                    .HasColumnName("Province");

                location.Property(p => p.LandOffice).UseLandOfficeConfig().HasColumnName("LandOffice");
            }
        );

        builder.Property(p => p.LandDesc).UseRemarkConfig();
    }
}
