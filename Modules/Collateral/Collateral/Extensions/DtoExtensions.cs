using Collateral.CollateralMasters.ValueObjects;
using Collateral.CollateralProperties.ValueObjects;

namespace Collateral.Extensions;

public static class DtoExtensions
{
    public static CollateralType ToDomain(this CollateralTypeDto dto)
    {
        return dto switch
        {
            CollateralTypeDto.Land => CollateralType.Land,
            CollateralTypeDto.Building => CollateralType.Building,
            CollateralTypeDto.Condo => CollateralType.Condo,
            CollateralTypeDto.Machine => CollateralType.Machine,
            CollateralTypeDto.Vehicle => CollateralType.Vehicle,
            CollateralTypeDto.Vessel => CollateralType.Vessel,
            _ => throw new ArgumentOutOfRangeException(nameof(dto)),
        };
    }

    public static CollateralLand ToDomain(this CollateralLandDto dto, long collatId)
    {
        return CollateralLand.Create(
            collatId,
            dto.Coordinate.ToDomain(),
            dto.CollateralLocation.ToDomain(),
            dto.LandDesc
        );
    }

    public static Coordinate ToDomain(this CoordinateDto dto)
    {
        return Coordinate.Create(dto.Latitude, dto.Longitude);
    }

    public static CollateralLocation ToDomain(this CollateralLocationDto dto)
    {
        return CollateralLocation.Create(
            dto.SubDistrict,
            dto.District,
            dto.Province,
            dto.LandOffice
        );
    }

    public static LandTitle ToDomain(this LandTitleDto dto, long collatId)
    {
        return LandTitle.Create(
            collatId,
            dto.SeqNo,
            dto.LandTitleDocumentDetail.ToDomain(),
            dto.LandTitleArea.ToDomain(),
            dto.DocumentType,
            dto.Rawang,
            dto.AerialPhotoNo,
            dto.BoundaryMarker,
            dto.BoundaryMarkerOther,
            dto.DocValidate,
            dto.PricePerSquareWa,
            dto.GovernmentPrice
        );
    }

    public static LandTitleDocumentDetail ToDomain(this LandTitleDocumentDetailDto dto)
    {
        return LandTitleDocumentDetail.Create(
            dto.TitleNo,
            dto.BookNo,
            dto.PageNo,
            dto.LandNo,
            dto.SurveyNo,
            dto.SheetNo
        );
    }

    public static LandTitleArea ToDomain(this LandTitleAreaDto dto)
    {
        return LandTitleArea.Create(dto.Rai, dto.Ngan, dto.Wa, dto.TotalAreaInSqWa);
    }

    public static CollateralBuilding ToDomain(this CollateralBuildingDto dto, long collatId)
    {
        return CollateralBuilding.Create(
            collatId,
            dto.BuildingNo,
            dto.ModelName,
            dto.HouseNo,
            dto.BuiltOnTitleNo,
            dto.Owner
        );
    }

    public static CollateralCondo ToDomain(this CollateralCondoDto dto, long collatId)
    {
        return CollateralCondo.Create(
            collatId,
            dto.CondoName,
            dto.BuildingNo,
            dto.ModelName,
            dto.BuiltOnTitleNo,
            dto.CondoRegisNo,
            dto.RoomNo,
            dto.FloorNo,
            dto.UsableArea,
            dto.CollateralLocation.ToDomain(),
            dto.Coordinate.ToDomain(),
            dto.Owner
        );
    }

    public static CollateralMachine ToDomain(this CollateralMachineDto dto, long collatId)
    {
        return CollateralMachine.Create(
            collatId,
            dto.CollateralMachineProperty.ToDomain(),
            dto.CollateralMachineDetail.ToDomain(),
            dto.CollateralMachineSize.ToDomain(),
            dto.ChassisNo
        );
    }

    public static CollateralVehicle ToDomain(this CollateralVehicleDto dto, long collatId)
    {
        return CollateralVehicle.Create(
            collatId,
            dto.CollateralVehicleProperty.ToDomain(),
            dto.CollateralVehicleDetail.ToDomain(),
            dto.CollateralVehicleSize.ToDomain(),
            dto.ChassisNo
        );
    }

    public static CollateralVessel ToDomain(this CollateralVesselDto dto, long collatId)
    {
        return CollateralVessel.Create(
            collatId,
            dto.CollateralVesselProperty.ToDomain(),
            dto.CollateralVesselDetail.ToDomain(),
            dto.CollateralVesselSize.ToDomain()
        );
    }

    public static CollateralDetail ToDomain(this CollateralDetailDto dto)
    {
        return CollateralDetail.Create(
            dto.EngineNo,
            dto.RegistrationNo,
            dto.YearOfManufacture,
            dto.CountryOfManufacture,
            dto.PurchaseDate,
            dto.PurchasePrice
        );
    }

    public static CollateralProperty ToDomain(this CollateralPropertyDto dto)
    {
        return CollateralProperty.Create(dto.Name, dto.Brand, dto.Model, dto.EnergyUse);
    }

    public static CollateralSize ToDomain(this CollateralSizeDto dto)
    {
        return CollateralSize.Create(dto.Capacity, dto.Width, dto.Length, dto.Height);
    }
}
