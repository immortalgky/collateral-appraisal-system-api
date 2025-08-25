namespace Shared.Dtos;

public record RequestTitleDto(
    long RequestId,
    string CollateralType,
    string? TitleNo,
    string? TitleDetail,
    string? Owner,
    LandAreaDto LandArea,
    string? BuildingType,
    decimal? UsageArea,
    int? NoOfBuilding,
    AddressDto TitleAddress,
    AddressDto DopaAddress,
    VehicleDto Vehicle,
    MachineDto Machine
);
