namespace Collateral.Application.Features.CollateralMasters.GetEngagements;

public record GetEngagementsResult(PaginatedResult<EngagementListItemDto> Engagements);

/// <summary>
/// Metadata-only projection from vw_CollateralEngagements — no snapshot column.
/// PR-4: PropertyId and AppraisedValue removed (engagement is now per-appraisal;
/// values live on master detail rows and inside the engagement Snapshot JSON).
/// </summary>
public record EngagementListItemDto(
    Guid Id,
    Guid CollateralMasterId,
    Guid AppraisalId,
    string AppraisalNumber,
    Guid RequestId,
    string RequestNumber,
    string AppraisalType,
    DateTime AppraisalDate,
    string? AppraiserUserId,
    Guid? AppraisalCompanyId,
    string? AppraisalCompanyName,
    decimal? ConstructionInspectionFeeAmount,
    DateTime CreatedAt,
    string CollateralType,
    string? OwnerName
);
