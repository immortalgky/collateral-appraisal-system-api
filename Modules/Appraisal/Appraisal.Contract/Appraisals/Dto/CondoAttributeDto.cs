namespace Appraisal.Contracts.Appraisals.Dto;

public record CondoAttributeDto(
    string? Decoration,
    short? BuildingYear,
    short CondoHeight,
    string? BuildingForm,
    string? ConstMaterial,
    CondoRoomLayoutDto CondoRoomLayout,
    CondoFloorDto CondoFloor,
    CondoRoofDto CondoRoof,
    decimal? TotalAreaInSqM
);
