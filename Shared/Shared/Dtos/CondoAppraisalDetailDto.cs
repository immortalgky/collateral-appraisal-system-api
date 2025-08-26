namespace Shared.Dtos;

public record CondoAppraisalDetailDto(
    long ApprId,
    ObligationDetailDto ObligationDetail,
    string? DocValidate,
    CondominiumLocationDto CondominiumLocation,
    CondoAttributeDto CondoAttribute,
    ExpropriationDto Expropriation,
    CondominiumFacilityDto CondominiumFacility,
    CondoPriceDto CondoPrice,
    ForestBoundaryDto ForestBoundary,
    string? Remark,
    IReadOnlyList<CondoAppraisalAreaDetailDto> CondoAppraisalAreaDetails
);
