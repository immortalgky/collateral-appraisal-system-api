namespace Collateral.Application.Features.CollateralMasters.GetEngagements;

public record GetEngagementsQuery(
    Guid CollateralMasterId,
    PaginationRequest PaginationRequest
) : IQuery<GetEngagementsResult>;
