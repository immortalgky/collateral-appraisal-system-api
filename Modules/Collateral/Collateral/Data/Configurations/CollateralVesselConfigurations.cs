namespace Collateral.Data.Configurations;

public class CollateralVesselConfigurations : IEntityTypeConfiguration<CollateralVessel>
{
    public void Configure(EntityTypeBuilder<CollateralVessel> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).UseIdentityColumn();
        builder.Property(p => p.Id).HasColumnName("VesselId");

        builder.OwnsOne(p => p.CollateralVesselProperty, vesselProperty =>
        {
            vesselProperty.Property(p => p.Name).UseNameConfig()
                .HasColumnName("VesselName");
            vesselProperty.Property(p => p.Brand).UseNameConfig()
                .HasColumnName("Brand");
            vesselProperty.Property(p => p.Model).UseNameConfig()
                .HasColumnName("Model");
            vesselProperty.Property(p => p.EnergyUse).UseMediumStringConfig()
                .HasColumnName("EnergyUse");
        });

        builder.OwnsOne(p => p.CollateralVesselDetail, vesselDetail =>
        {
            vesselDetail.Property(p => p.EngineNo).UseShortStringConfig()
                .HasColumnName("EngineNo");
            vesselDetail.Property(p => p.RegistrationNo).HasMaxLength(30)
                .HasColumnName("RegistrationNo");
            vesselDetail.Property(p => p.YearOfManufacture)
                .HasColumnName("YearOfManufacture");
            vesselDetail.Property(p => p.CountryOfManufacture).UseCodeConfig()
                .HasColumnName("CountryOfManufacture");
            vesselDetail.Property(p => p.PurchaseDate).HasMaxLength(30)
                .HasColumnName("PurchaseDate");
            vesselDetail.Property(p => p.PurchasePrice).HasPrecision(19, 4)
                .HasColumnName("PurchasePrice");
        });

        builder.OwnsOne(p => p.CollateralVesselSize, vesselSize =>
        {
            vesselSize.Property(p => p.Capacity).UseMediumStringConfig()
                .HasColumnName("Capacity");
            vesselSize.Property(p => p.Width).UseSizeConfig()
                .HasColumnName("Width");
            vesselSize.Property(p => p.Length).UseSizeConfig()
                .HasColumnName("Length");
            vesselSize.Property(p => p.Height).UseSizeConfig()
                .HasColumnName("Height");
        });
    }
}