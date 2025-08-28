namespace Collateral.Collateral.Shared.Features.DeleteCollateral;

public class DeleteCollateralCommandHandler(ICollateralRepository collateralRepository)
    : ICommandHandler<DeleteCollateralCommand, DeleteCollateralResult>
{
    public async Task<DeleteCollateralResult> Handle(
        DeleteCollateralCommand command,
        CancellationToken cancellationToken
    )
    {
        await collateralRepository.DeleteCollateralMasterAsync(command.Id, cancellationToken);
        await collateralRepository.SaveChangesAsync(cancellationToken);
        return new DeleteCollateralResult(true);
    }
}
