using Request.RequestTitles.Models;

namespace Request.Data.Configurations;

public class RequestTitleConfiguration : IEntityTypeConfiguration<RequestTitle>
{
    public void Configure(EntityTypeBuilder<RequestTitle> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .UseIdentityColumn();

        builder.Property(p => p.CollateralType)
            .HasMaxLength(10);

        builder.Property(p => p.TitleNo)
            .HasMaxLength(20);

        builder.Property(p => p.TitleDetail)
            .HasMaxLength(200);

        builder.Property(p => p.Owner)
            .HasMaxLength(80);

        builder.OwnsOne(p => p.LandArea, landArea =>
        {
            landArea.Property(p => p.Rai)
                .HasColumnName("Rai");

            landArea.Property(p => p.Ngan)
                .HasColumnName("Ngan");

            landArea.Property(p => p.Wa)
                .HasPrecision(4, 2)
                .HasColumnName("Wa");
        });

        builder.Property(p => p.BuildingType)
            .HasMaxLength(10);

        builder.Property(p => p.UsageArea)
            .HasPrecision(19, 4);

        builder.OwnsOne(p => p.TitleAddress, titleAddress =>
        {
            titleAddress.Property(p => p.HouseNo)
                .HasMaxLength(30)
                .HasColumnName("HouseNo");

            titleAddress.Property(p => p.RoomNo)
                .HasMaxLength(30)
                .HasColumnName("RoomNo");

            titleAddress.Property(p => p.FloorNo)
                .HasMaxLength(10)
                .HasColumnName("FloorNo");

            titleAddress.Property(p => p.BuildingNo)
                .HasMaxLength(50)
                .HasColumnName("BuildingNo");

            titleAddress.Property(p => p.Moo)
                .HasMaxLength(50)
                .HasColumnName("Moo");

            titleAddress.Property(p => p.Soi)
                .HasMaxLength(50)
                .HasColumnName("Soi");

            titleAddress.Property(p => p.Road)
                .HasMaxLength(50)
                .HasColumnName("Road");

            titleAddress.Property(p => p.SubDistrict)
                .HasMaxLength(10)
                .HasColumnName("SubDistrict");

            titleAddress.Property(p => p.District)
                .HasMaxLength(10)
                .HasColumnName("District");

            titleAddress.Property(p => p.Province)
                .HasMaxLength(10)
                .HasColumnName("Province");

            titleAddress.Property(p => p.Postcode)
                .HasMaxLength(10)
                .HasColumnName("Postcode");
        });

        builder.OwnsOne(p => p.DopaAddress, dopaAddress =>
        {
            dopaAddress.Property(p => p.HouseNo)
                .HasMaxLength(30)
                .HasColumnName("DopaHouseNo");

            dopaAddress.Property(p => p.RoomNo)
                .HasMaxLength(30)
                .HasColumnName("DopaRoomNo");

            dopaAddress.Property(p => p.FloorNo)
                .HasMaxLength(10)
                .HasColumnName("DopaFloorNo");

            dopaAddress.Property(p => p.BuildingNo)
                .HasMaxLength(50)
                .HasColumnName("DopaBuildingNo");

            dopaAddress.Property(p => p.Moo)
                .HasMaxLength(50)
                .HasColumnName("DopaMoo");

            dopaAddress.Property(p => p.Soi)
                .HasMaxLength(50)
                .HasColumnName("DopaSoi");

            dopaAddress.Property(p => p.Road)
                .HasMaxLength(50)
                .HasColumnName("DopaRoad");

            dopaAddress.Property(p => p.SubDistrict)
                .HasMaxLength(10)
                .HasColumnName("DopaSubDistrict");

            dopaAddress.Property(p => p.District)
                .HasMaxLength(10)
                .HasColumnName("DopaDistrict");

            dopaAddress.Property(p => p.Province)
                .HasMaxLength(10)
                .HasColumnName("DopaProvince");

            dopaAddress.Property(p => p.Postcode)
                .HasMaxLength(10)
                .HasColumnName("DopaPostcode");
        });

        builder.OwnsOne(p => p.Vehicle, vehicle =>
        {
            vehicle.Property(p => p.VehicleType)
                .HasMaxLength(10)
                .HasColumnName("VehicleType");

            vehicle.Property(p => p.VehicleRegistrationNo)
                .HasMaxLength(20)
                .HasColumnName("VehicleRegistrationNo");

            vehicle.Property(p => p.VehicleLocation)
                .HasMaxLength(300)
                .HasColumnName("VehicleLocation");
        });

        builder.OwnsOne(p => p.Machine, machine =>
        {
            machine.Property(p => p.MachineStatus)
                .HasMaxLength(10)
                .HasColumnName("MachineStatus");

            machine.Property(p => p.MachineType)
                .HasMaxLength(10)
                .HasColumnName("MachineType");

            machine.Property(p => p.MachineRegistrationStatus)
                .HasMaxLength(10)
                .HasColumnName("MachineRegistrationStatus");

            machine.Property(p => p.MachineRegistrationNo)
                .HasMaxLength(50)
                .HasColumnName("MachineRegistrationNo");

            machine.Property(p => p.MachineInvoiceNo)
                .HasMaxLength(20)
                .HasColumnName("MachineInvoiceNo");
        });
    }
}