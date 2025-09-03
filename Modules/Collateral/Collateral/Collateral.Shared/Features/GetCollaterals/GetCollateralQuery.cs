namespace Collateral.Collateral.Shared.Features.GetCollaterals;

public record GetCollateralQuery(GetCollateralRequest GetCollateralRequest)
    : IQuery<GetCollateralResult>;
