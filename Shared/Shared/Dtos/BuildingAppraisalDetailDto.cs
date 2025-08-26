namespace Shared.Dtos;

public record BuildingAppraisalDetailDto(
    long ApprId,
    BuildingInformationDto BuildingInformation,
    BuildingTypeDetailDto BuildingTypeDetail,
    DecorationDetailDto DecorationDetail,
    EncroachmentDto Encroachment,
    BuildingConstructionInformationDto BuildingConstructionInformation,
    string? BuildingMaterial,
    string? BuildingStyle,
    ResidentialStatusDto ResidentialStatus,
    BuildingStructureDetailDto BuildingStructureDetail,
    UtilizationDetailDto UtilizationDetail,
    string? Remark,
    IReadOnlyList<BuildingAppraisalSurfaceDto> BuildingAppraisalSurfaces,
    IReadOnlyList<BuildingAppraisalDepreciationDetailDto> BuildingAppraisalDepreciationDetails
);
