namespace Collateral.Collateral.Shared.Features.DeleteCollateral;

public class DeleteCollateralCommandHandler(ICollateralService collateralService)
    : ICommandHandler<DeleteCollateralCommand, DeleteCollateralResult>
{
    public async Task<DeleteCollateralResult> Handle(
        DeleteCollateralCommand command,
        CancellationToken cancellationToken
    )
    {
        await collateralService.DeleteCollateral(command.Id, cancellationToken);
        return new DeleteCollateralResult(true);
    }
}
