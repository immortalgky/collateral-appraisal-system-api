namespace Shared.Dtos;

public record CollateralBuildingDto(
    long CollatId,
    string BuildingNo,
    string ModelName,
    string HouseNo,
    string BuiltOnTitleNo,
    string Owner
);
