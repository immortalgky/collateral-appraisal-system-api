namespace Collateral.Collateral.Shared.Features.UpdateCollateralEngagement;

public record UpdateCollateralEngagementCommand(long CollatId, long? ReqId)
    : ICommand<UpdateCollateralEngagementResult>;
