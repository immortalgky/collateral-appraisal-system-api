namespace Collateral.Collateral.Shared.Features.AddCollateralRequestId;

public record AddCollateralRequestIdCommand(long CollatId, long ReqId)
    : ICommand<AddCollateralRequestIdResult>;
