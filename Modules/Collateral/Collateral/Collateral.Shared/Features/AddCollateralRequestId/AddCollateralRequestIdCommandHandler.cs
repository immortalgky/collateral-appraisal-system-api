namespace Collateral.Collateral.Shared.Features.AddCollateralRequestId;

public class AddCollateralRequestIdCommandHandler(ICollateralService collateralService)
    : ICommandHandler<AddCollateralRequestIdCommand, AddCollateralRequestIdResult>
{
    public async Task<AddCollateralRequestIdResult> Handle(
        AddCollateralRequestIdCommand command,
        CancellationToken cancellationToken
    )
    {
        await collateralService.AddCollateralRequestId(
            command.CollatId,
            command.ReqId,
            cancellationToken
        );
        return new AddCollateralRequestIdResult(true);
    }
}
