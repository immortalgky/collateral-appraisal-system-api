namespace Shared.Dtos;

public record CollateralCondoDto(
    long CollatId,
    string CondoName,
    string BuildingNo,
    string ModelName,
    string BuiltOnTitleNo,
    string CondoRegisNo,
    string RoomNo,
    int FloorNo,
    decimal UsableArea,
    CollateralLocationDto CollateralLocation,
    CoordinateDto Coordinate,
    string Owner
);
