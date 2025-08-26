namespace Shared.Dtos;

public record CondoFloorDto(
    string? GroundFloorMaterial,
    string? GroundFloorMaterialOther,
    string? UpperFloorMaterial,
    string? UpperFloorMaterialOther,
    string? BathroomFloorMaterial,
    string? BathroomFloorMaterialOther
);
