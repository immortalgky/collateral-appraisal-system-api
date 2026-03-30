namespace Appraisal.Contracts.Appraisals.Dto;

public record BuildingStructureDetailDto(
    BuildingConstructionStyleDto BuildingConstructionStyle,
    BuildingGeneralStructureDto BuildingGeneralStructure,
    BuildingRoofFrameDto BuildingRoofFrame,
    BuildingRoofDto BuildingRoof,
    BuildingCeilingDto BuildingCeiling,
    BuildingWallDto BuildingWall,
    BuildingFenceDto BuildingFence,
    BuildingConstructionTypeDto ConstType
);
