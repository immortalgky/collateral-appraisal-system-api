namespace Collateral.Data.Configurations;

public class CollateralVehicleConfigurations : IEntityTypeConfiguration<CollateralVehicle>
{
    public void Configure(EntityTypeBuilder<CollateralVehicle> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).UseIdentityColumn();
        builder.Property(p => p.Id).HasColumnName("VehicleId");

        builder.Property(p => p.ChassisNo).HasColumnName("ChassisNo")
            .HasMaxLength(25);

        builder.OwnsOne(p => p.CollateralVehicleProperty, vehicleProperty =>
        {
            vehicleProperty.Property(p => p.Name).UseNameConfig()
                .HasColumnName("VehicleName");
            vehicleProperty.Property(p => p.Brand).UseNameConfig()
                .HasColumnName("Brand");
            vehicleProperty.Property(p => p.Model).UseNameConfig()
                .HasColumnName("Model");
            vehicleProperty.Property(p => p.EnergyUse).UseMediumStringConfig()
                .HasColumnName("EnergyUse");
        });

        builder.OwnsOne(p => p.CollateralVehicleDetail, vehicleDetail =>
        {
            vehicleDetail.Property(p => p.EngineNo).UseShortStringConfig()
                .HasColumnName("EngineNo");
            vehicleDetail.Property(p => p.RegistrationNo).HasMaxLength(30)
                .HasColumnName("RegistrationNo");
            vehicleDetail.Property(p => p.YearOfManufacture)
                .HasColumnName("YearOfManufacture");
            vehicleDetail.Property(p => p.CountryOfManufacture).UseCodeConfig()
                .HasColumnName("CountryOfManufacture");
            vehicleDetail.Property(p => p.PurchaseDate).HasMaxLength(30)
                .HasColumnName("PurchaseDate");
            vehicleDetail.Property(p => p.PurchasePrice).HasPrecision(19, 4)
                .HasColumnName("PurchasePrice");
        });

        builder.OwnsOne(p => p.CollateralVehicleSize, vehicleSize =>
        {
            vehicleSize.Property(p => p.Capacity).UseMediumStringConfig()
                .HasColumnName("Capacity");
            vehicleSize.Property(p => p.Width).UseSizeConfig()
                .HasColumnName("Width");
            vehicleSize.Property(p => p.Length).UseSizeConfig()
                .HasColumnName("Length");
            vehicleSize.Property(p => p.Height).UseSizeConfig()
                .HasColumnName("Height");
        });
    }
}