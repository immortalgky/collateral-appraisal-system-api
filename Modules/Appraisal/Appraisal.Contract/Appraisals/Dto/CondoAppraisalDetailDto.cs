namespace Appraisal.Contracts.Appraisals.Dto;

public record CondoAppraisalDetailDto(
    long ApprId,
    ObligationDetailDto ObligationDetail,
    string? IsDocumentValidated,
    CondominiumLocationDto CondominiumLocation,
    CondoAttributeDto CondoAttribute,
    ExpropriationDto Expropriation,
    CondominiumFacilityDto CondominiumFacility,
    CondoPriceDto CondoPrice,
    ForestBoundaryDto ForestBoundary,
    string? Remark,
    IReadOnlyList<CondoAppraisalAreaDetailDto> CondoAppraisalAreaDetails
);
