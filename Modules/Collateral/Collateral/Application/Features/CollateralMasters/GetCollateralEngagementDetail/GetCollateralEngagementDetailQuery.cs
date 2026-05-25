namespace Collateral.Application.Features.CollateralMasters.GetCollateralEngagementDetail;

public record GetCollateralEngagementDetailQuery(
    Guid CollateralMasterId,
    Guid EngagementId
) : IQuery<GetCollateralEngagementDetailResult>;
