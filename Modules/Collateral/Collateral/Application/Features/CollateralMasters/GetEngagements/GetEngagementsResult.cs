namespace Collateral.Application.Features.CollateralMasters.GetEngagements;

public record GetEngagementsResult(PaginatedResult<EngagementListItemDto> Engagements);

/// <summary>
/// Metadata-only projection from vw_CollateralEngagements — no snapshot column.
/// </summary>
public record EngagementListItemDto(
    Guid Id,
    Guid CollateralMasterId,
    Guid AppraisalId,
    string AppraisalNumber,
    Guid RequestId,
    string RequestNumber,
    Guid PropertyId,
    string AppraisalType,
    DateTime AppraisalDate,
    decimal? AppraisedValue,
    string? AppraiserUserId,
    Guid? AppraisalCompanyId,
    string? AppraisalCompanyName,
    DateTime CreatedOn,
    string CollateralType,
    string? OwnerName
);
