namespace Request.Infrastructure.Configurations;

public class RequestTitleConfiguration : IEntityTypeConfiguration<RequestTitle>
{
    public void Configure(EntityTypeBuilder<RequestTitle> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasDiscriminator<string>(nameof(CollateralType))
            .HasValue<TitleLand>(CollateralType.Land)
            .HasValue<TitleBuilding>(CollateralType.Building)
            .HasValue<TitleLandBuilding>(CollateralType.LandAndBuilding)
            .HasValue<TitleCondo>(CollateralType.Condo)
            .HasValue<TitleLeaseAgreementLand>(CollateralType.LeaseAgreementLand)
            .HasValue<TitleLeaseAgreementBuilding>(CollateralType.LeaseAgreementBuilding)
            .HasValue<TitleLeaseAgreementLandBuilding>(CollateralType.LeaseAgreementLandAndBuilding)
            .HasValue<TitleLeaseAgreementCondo>(CollateralType.LeaseAgreementCondo)
            .HasValue<TitleMachine>(CollateralType.Machine)
            .HasValue<TitleVehicle>(CollateralType.Vehicle)
            .HasValue<TitleVessel>(CollateralType.Vessel);

        builder.Property(p => p.CollateralType)
            .HasMaxLength(10);

        builder.Property(p => p.CollateralStatus);

        builder.Property(p => p.OwnerName)
            .HasMaxLength(500);

        builder.OwnsOne(p => p.TitleAddress, titleAddress =>
        {
            titleAddress.Property(p => p.HouseNumber)
                .HasMaxLength(30)
                .HasColumnName("HouseNumber");

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
            dopaAddress.Property(p => p.HouseNumber)
                .HasMaxLength(30)
                .HasColumnName("DopaHouseNumber");

            dopaAddress.Property(p => p.ProjectName)
                .HasMaxLength(100)
                .HasColumnName("DopaProjectName");

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

        builder.HasIndex(p => p.RequestId)
            .HasDatabaseName("IX_TitleDeedInfo_RequestId");

        // TitleDocuments
        builder.OwnsMany(t => t.Documents, doc =>
            new TitleDocumentConfiguration().Configure(doc));
    }
}

// Configuration for TitleLand
public class TitleLandConfiguration : IEntityTypeConfiguration<TitleLand>
{
    public void Configure(EntityTypeBuilder<TitleLand> builder)
    {
        builder.OwnsOne(p => p.TitleDeedInfo, titleDeedInfo =>
        {
            titleDeedInfo.Property(p => p.TitleNumber)
                .HasMaxLength(200)
                .HasColumnName("TitleNumber");

            titleDeedInfo.Property(p => p.TitleType)
                .HasMaxLength(50)
                .HasColumnName("TitleType");

            titleDeedInfo.HasIndex(p => p.TitleNumber)
                .HasDatabaseName("IX_TitleDeedInfo_TitleDeedNumber");
        });

        builder.OwnsOne(p => p.LandLocationInfo, landLocation =>
        {
            landLocation.Property(p => p.BookNumber)
                .HasMaxLength(100)
                .HasColumnName("BookNumber");

            landLocation.Property(p => p.PageNumber)
                .HasMaxLength(100)
                .HasColumnName("PageNumber");

            landLocation.Property(p => p.LandParcelNumber)
                .HasMaxLength(100)
                .HasColumnName("LandParcelNumber");

            landLocation.Property(p => p.SurveyNumber)
                .HasMaxLength(100)
                .HasColumnName("SurveyNumber");

            landLocation.Property(p => p.MapSheetNumber)
                .HasMaxLength(100)
                .HasColumnName("MapSheetNumber");

            landLocation.Property(p => p.Rawang)
                .HasMaxLength(100)
                .HasColumnName("Rawang");

            landLocation.Property(p => p.AerialMapName)
                .HasMaxLength(100)
                .HasColumnName("AerialMapName");

            landLocation.Property(p => p.AerialMapNumber)
                .HasMaxLength(100)
                .HasColumnName("AerialMapNumber");
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
    }
}

// Configuration for TitleBuilding
public class TitleBuildingConfiguration : IEntityTypeConfiguration<TitleBuilding>
{
    public void Configure(EntityTypeBuilder<TitleBuilding> builder)
    {
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
    }
}

// Configuration for TitleLandBuilding
public class TitleLandBuildingConfiguration : IEntityTypeConfiguration<TitleLandBuilding>
{
    public void Configure(EntityTypeBuilder<TitleLandBuilding> builder)
    {
        builder.OwnsOne(p => p.TitleDeedInfo, titleDeedInfo =>
        {
            titleDeedInfo.Property(p => p.TitleNumber)
                .HasMaxLength(200)
                .HasColumnName("TitleNumber");

            titleDeedInfo.Property(p => p.TitleType)
                .HasMaxLength(50)
                .HasColumnName("TitleType");
        });

        builder.OwnsOne(p => p.LandLocationInfo, landLocation =>
        {
            landLocation.Property(p => p.BookNumber)
                .HasMaxLength(100)
                .HasColumnName("BookNumber");

            landLocation.Property(p => p.PageNumber)
                .HasMaxLength(100)
                .HasColumnName("PageNumber");

            landLocation.Property(p => p.LandParcelNumber)
                .HasMaxLength(100)
                .HasColumnName("LandParcelNumber");

            landLocation.Property(p => p.SurveyNumber)
                .HasMaxLength(100)
                .HasColumnName("SurveyNumber");

            landLocation.Property(p => p.MapSheetNumber)
                .HasMaxLength(100)
                .HasColumnName("MapSheetNumber");

            landLocation.Property(p => p.Rawang)
                .HasMaxLength(100)
                .HasColumnName("Rawang");

            landLocation.Property(p => p.AerialMapName)
                .HasMaxLength(100)
                .HasColumnName("AerialMapName");

            landLocation.Property(p => p.AerialMapNumber)
                .HasMaxLength(100)
                .HasColumnName("AerialMapNumber");
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
    }
}

// Configuration for TitleCondo
public class TitleCondoConfiguration : IEntityTypeConfiguration<TitleCondo>
{
    public void Configure(EntityTypeBuilder<TitleCondo> builder)
    {
        builder.OwnsOne(p => p.TitleDeedInfo, titleDeedInfo =>
        {
            titleDeedInfo.Property(p => p.TitleNumber)
                .HasMaxLength(200)
                .HasColumnName("TitleNumber");

            titleDeedInfo.Property(p => p.TitleType)
                .HasMaxLength(50)
                .HasColumnName("TitleType");
        });

        builder.OwnsOne(p => p.CondoInfo, condoInfo =>
        {
            condoInfo.Property(p => p.CondoName)
                .HasMaxLength(100)
                .HasColumnName("CondoName");

            condoInfo.Property(p => p.BuildingNumber)
                .HasMaxLength(100)
                .HasColumnName("BuildingNumber");

            condoInfo.Property(p => p.RoomNumber)
                .HasMaxLength(30)
                .HasColumnName("RoomNumber");

            condoInfo.Property(p => p.FloorNumber)
                .HasMaxLength(10)
                .HasColumnName("FloorNumber");

            condoInfo.Property(p => p.UsableArea)
                .HasPrecision(19, 4)
                .HasColumnName("UsableArea");
        });
    }
}

// Configuration for TitleLeaseAgreementLand
public class TitleLeaseAgreementLandConfiguration : IEntityTypeConfiguration<TitleLeaseAgreementLand>
{
    public void Configure(EntityTypeBuilder<TitleLeaseAgreementLand> builder)
    {
        builder.OwnsOne(p => p.TitleDeedInfo, titleDeedInfo =>
        {
            titleDeedInfo.Property(p => p.TitleNumber)
                .HasMaxLength(200)
                .HasColumnName("TitleNumber");

            titleDeedInfo.Property(p => p.TitleType)
                .HasMaxLength(50)
                .HasColumnName("TitleType");
        });

        builder.OwnsOne(p => p.LandLocationInfo, landLocation =>
        {
            landLocation.Property(p => p.BookNumber)
                .HasMaxLength(100)
                .HasColumnName("BookNumber");

            landLocation.Property(p => p.PageNumber)
                .HasMaxLength(100)
                .HasColumnName("PageNumber");

            landLocation.Property(p => p.LandParcelNumber)
                .HasMaxLength(100)
                .HasColumnName("LandParcelNumber");

            landLocation.Property(p => p.SurveyNumber)
                .HasMaxLength(100)
                .HasColumnName("SurveyNumber");

            landLocation.Property(p => p.MapSheetNumber)
                .HasMaxLength(100)
                .HasColumnName("MapSheetNumber");

            landLocation.Property(p => p.Rawang)
                .HasMaxLength(100)
                .HasColumnName("Rawang");

            landLocation.Property(p => p.AerialMapName)
                .HasMaxLength(100)
                .HasColumnName("AerialMapName");

            landLocation.Property(p => p.AerialMapNumber)
                .HasMaxLength(100)
                .HasColumnName("AerialMapNumber");
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
    }
}

// Configuration for TitleLeaseAgreementBuilding
public class TitleLeaseAgreementBuildingConfiguration : IEntityTypeConfiguration<TitleLeaseAgreementBuilding>
{
    public void Configure(EntityTypeBuilder<TitleLeaseAgreementBuilding> builder)
    {
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
    }
}

// Configuration for TitleLeaseAgreementLandBuilding
public class TitleLeaseAgreementLandBuildingConfiguration : IEntityTypeConfiguration<TitleLeaseAgreementLandBuilding>
{
    public void Configure(EntityTypeBuilder<TitleLeaseAgreementLandBuilding> builder)
    {
        builder.OwnsOne(p => p.TitleDeedInfo, titleDeedInfo =>
        {
            titleDeedInfo.Property(p => p.TitleNumber)
                .HasMaxLength(200)
                .HasColumnName("TitleNumber");

            titleDeedInfo.Property(p => p.TitleType)
                .HasMaxLength(50)
                .HasColumnName("TitleType");
        });

        builder.OwnsOne(p => p.LandLocationInfo, landLocation =>
        {
            landLocation.Property(p => p.BookNumber)
                .HasMaxLength(100)
                .HasColumnName("BookNumber");

            landLocation.Property(p => p.PageNumber)
                .HasMaxLength(100)
                .HasColumnName("PageNumber");

            landLocation.Property(p => p.LandParcelNumber)
                .HasMaxLength(100)
                .HasColumnName("LandParcelNumber");

            landLocation.Property(p => p.SurveyNumber)
                .HasMaxLength(100)
                .HasColumnName("SurveyNumber");

            landLocation.Property(p => p.MapSheetNumber)
                .HasMaxLength(100)
                .HasColumnName("MapSheetNumber");

            landLocation.Property(p => p.Rawang)
                .HasMaxLength(100)
                .HasColumnName("Rawang");

            landLocation.Property(p => p.AerialMapName)
                .HasMaxLength(100)
                .HasColumnName("AerialMapName");

            landLocation.Property(p => p.AerialMapNumber)
                .HasMaxLength(100)
                .HasColumnName("AerialMapNumber");
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
    }
}

// Configuration for TitleLeaseAgreementCondo
public class TitleLeaseAgreementCondoConfiguration : IEntityTypeConfiguration<TitleLeaseAgreementCondo>
{
    public void Configure(EntityTypeBuilder<TitleLeaseAgreementCondo> builder)
    {
        builder.OwnsOne(p => p.TitleDeedInfo, titleDeedInfo =>
        {
            titleDeedInfo.Property(p => p.TitleNumber)
                .HasMaxLength(200)
                .HasColumnName("TitleNumber");

            titleDeedInfo.Property(p => p.TitleType)
                .HasMaxLength(50)
                .HasColumnName("TitleType");
        });

        builder.OwnsOne(p => p.CondoInfo, condoInfo =>
        {
            condoInfo.Property(p => p.CondoName)
                .HasMaxLength(100)
                .HasColumnName("CondoName");

            condoInfo.Property(p => p.BuildingNumber)
                .HasMaxLength(100)
                .HasColumnName("BuildingNumber");

            condoInfo.Property(p => p.RoomNumber)
                .HasMaxLength(30)
                .HasColumnName("RoomNumber");

            condoInfo.Property(p => p.FloorNumber)
                .HasMaxLength(10)
                .HasColumnName("FloorNumber");

            condoInfo.Property(p => p.UsableArea)
                .HasPrecision(19, 4)
                .HasColumnName("UsableArea");
        });
    }
}

// Configuration for TitleVehicle
public class TitleVehicleConfiguration : IEntityTypeConfiguration<TitleVehicle>
{
    public void Configure(EntityTypeBuilder<TitleVehicle> builder)
    {
        builder.OwnsOne(p => p.VehicleInfo, vehicle =>
        {
            vehicle.Property(p => p.VehicleType)
                .HasMaxLength(10)
                .HasColumnName("VehicleType");

            vehicle.Property(p => p.VehicleLocation)
                .HasMaxLength(300)
                .HasColumnName("VehicleLocation");

            vehicle.Property(p => p.VIN)
                .HasMaxLength(50)
                .HasColumnName("VIN");

            vehicle.Property(p => p.LicensePlateNumber)
                .HasMaxLength(20)
                .HasColumnName("LicensePlateNumber");
        });
    }
}

// Configuration for TitleVessel
public class TitleVesselConfiguration : IEntityTypeConfiguration<TitleVessel>
{
    public void Configure(EntityTypeBuilder<TitleVessel> builder)
    {
        builder.OwnsOne(p => p.VesselInfo, vessel =>
        {
            vessel.Property(p => p.VesselType)
                .HasMaxLength(10)
                .HasColumnName("VesselType");

            vessel.Property(p => p.VesselLocation)
                .HasMaxLength(300)
                .HasColumnName("VesselLocation");

            vessel.Property(p => p.HIN)
                .HasMaxLength(50)
                .HasColumnName("HIN");

            vessel.Property(p => p.VesselRegistrationNumber)
                .HasMaxLength(50)
                .HasColumnName("VesselRegistrationNumber");
        });
    }
}

// Configuration for TitleMachine
public class TitleMachineConfiguration : IEntityTypeConfiguration<TitleMachine>
{
    public void Configure(EntityTypeBuilder<TitleMachine> builder)
    {
        builder.OwnsOne(p => p.MachineInfo, machinery =>
        {
            machinery.Property(p => p.RegistrationNumber)
                .HasMaxLength(50)
                .HasColumnName("RegistrationNumber");

            machinery.Property(p => p.MachineType)
                .HasMaxLength(10)
                .HasColumnName("MachineType");

            machinery.Property(p => p.InstallationStatus)
                .HasMaxLength(10)
                .HasColumnName("InstallationStatus");

            machinery.Property(p => p.InvoiceNumber)
                .HasMaxLength(20)
                .HasColumnName("InvoiceNumber");

            machinery.Property(p => p.NumberOfMachine)
                .HasColumnName("NumberOfMachine");
        });
    }
}