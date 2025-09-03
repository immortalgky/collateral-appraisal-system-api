namespace Collateral.Collateral.Shared.Features.UpdateCollateralEngagement;

public class UpdateCollateralEngagementCommandHandler(ICollateralService collateralService)
    : ICommandHandler<UpdateCollateralEngagementCommand, UpdateCollateralEngagementResult>
{
    public async Task<UpdateCollateralEngagementResult> Handle(
        UpdateCollateralEngagementCommand command,
        CancellationToken cancellationToken
    )
    {
        if (!command.ReqId.HasValue || command.ReqId == 0)
        {
            await collateralService.DeactivateCollateralEngagement(
                command.CollatId,
                cancellationToken
            );
        }
        else
        {
            await collateralService.SetOrAddActiveCollateralEngagement(
                command.CollatId,
                command.ReqId.Value,
                cancellationToken
            );
        }
        return new UpdateCollateralEngagementResult(true);
    }
}
