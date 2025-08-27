namespace Appraisal.Contracts.Appraisals.Dto;

public record LandAppraisalDetailDto(
    string? PropertyName,
    string? CheckOwner,
    string? Owner,
    ObligationDetailDto ObligationDetail,
    LandLocationDetailDto LandLocationDetail,
    LandFillDetailDto LandFillDetail,
    LandAccessibilityDetailDto LandAccessibilityDetail,
    string? AnticipationOfProp,
    LandLimitationDto LandLimitation,
    string? Eviction,
    string? Allocation,
    ConsecutiveAreaDto ConsecutiveArea,
    LandMiscellaneousDetailDto LandMiscellaneousDetail
);
