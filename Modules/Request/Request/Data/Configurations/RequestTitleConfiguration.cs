using Request.RequestTitles.Models;

namespace Request.Data.Configurations;

public class RequestTitleConfiguration : IEntityTypeConfiguration<RequestTitle>
{
    public void Configure(EntityTypeBuilder<RequestTitle> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.HasMany(r => r.RequestTitleDocuments)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.CollateralType)
            .HasMaxLength(10);

        builder.Property(p => p.CollateralStatus);

        builder.OwnsOne(p => p.TitleDeedInfo, titleDeedInfo =>
        {
            titleDeedInfo.Property(p => p.TitleNo)
                .HasMaxLength(200)
                .HasColumnName("TitleNo");

            titleDeedInfo.Property(p => p.DeedType)
                .HasMaxLength(50)
                .HasColumnName("DeedType");

            titleDeedInfo.Property(p => p.TitleDetail)
                .HasMaxLength(200)
                .HasColumnName("TitleDetail");
            titleDeedInfo.HasIndex(p => p.TitleNo)
            .HasDatabaseName("IX_TitleDeedInfo_TitleDeedNumber");
        });

        builder.OwnsOne(p => p.SurveyInfo, surveyInfo =>
        {
            surveyInfo.Property(p => p.Rawang)
                .HasMaxLength(100)
                .HasColumnName("Rawang");

            surveyInfo.Property(p => p.LandNo)
                .HasMaxLength(50)
                .HasColumnName("LandNo");

            surveyInfo.Property(p => p.SurveyNo)
                .HasMaxLength(50)
                .HasColumnName("SurveyNo");
        });

        builder.OwnsOne(p => p.LandArea, landArea =>
        {
            landArea.Property(p => p.AreaRai)
                .HasColumnName("AreaRai");

            landArea.Property(p => p.AreaNgan)
                .HasColumnName("AreaNgan");

            landArea.Property(p => p.AreaSquareWa)
                .HasPrecision(5, 2)
                .HasColumnName("AreaSquareWa");
        });

        builder.Property(p => p.OwnerName)
            .HasMaxLength(500);

        builder.OwnsOne(p => p.BuildingInfo, buildingInfo =>
        {
            buildingInfo.Property(p => p.BuildingType)
                .HasMaxLength(10)
                .HasColumnName("BuildingType");

            buildingInfo.Property(p => p.UsableArea)
                .HasPrecision(19, 4)
                .HasColumnName("UsableArea");

            buildingInfo.Property(p => p.NumberOfBuilding)
                .HasColumnName("NumberOfBuilding");
        });

        builder.Property(p => p.RegistrationNo)
            .HasMaxLength(50);
        
        builder.OwnsOne(p => p.VehicleInfo, vehicle =>
        {
            vehicle.Property(p => p.VehicleType)
                .HasMaxLength(10)
                .HasColumnName("VehicleType");

            vehicle.Property(p => p.VehicleAppointmentLocation)
                .HasMaxLength(300)
                .HasColumnName("VehicleAppointmentLocation");
            
            vehicle.Property(p => p.ChassisNumber)
                .HasMaxLength(50)
                .HasColumnName("ChassisNumber");
        });

        builder.OwnsOne(p => p.MachineInfo, machinery =>
        {
            machinery.Property(p => p.MachineType)
                .HasMaxLength(10)
                .HasColumnName("MachineType");
            
            machinery.Property(p => p.MachineStatus)
                .HasMaxLength(10)
                .HasColumnName("MachineStatus");

            machinery.Property(p => p.InstallationStatus)
                .HasMaxLength(10)
                .HasColumnName("InstallationStatus");

            machinery.Property(p => p.InvoiceNumber)
                .HasMaxLength(20)
                .HasColumnName("InvoiceNumber");

            machinery.Property(p => p.NumberOfMachinery)
                .HasColumnName("NumberOfMachinery");
        });

        builder.OwnsOne(p => p.CondoInfo, condoInfo =>
        {
            condoInfo.Property(p => p.CondoName)
                .HasMaxLength(100)
                .HasColumnName("CondoName");
                
            condoInfo.Property(p => p.BuildingNo)
                .HasMaxLength(100)
                .HasColumnName("BuildingNo");
                
            condoInfo.Property(p => p.RoomNo)
                .HasMaxLength(30)
                .HasColumnName("RoomNo");
                
            condoInfo.Property(p => p.FloorNo)
                .HasMaxLength(10)
                .HasColumnName("FloorNo");
        });
        
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

            titleAddress.Property(p => p.ProjectName)
                .HasMaxLength(100)
                .HasColumnName("ProjectName");

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
            
            dopaAddress.Property(p => p.ProjectName)
                .HasMaxLength(100)
                .HasColumnName("ProjectName");

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

        builder.Property(p => p.Notes);

        builder.HasIndex(p => p.RequestId)
            .HasDatabaseName("IX_TitleDeedInfo_RequestId");
    }
}