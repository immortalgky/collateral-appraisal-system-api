namespace Collateral.Collateral.Shared.Features.GetCollateralById;

public record GetCollateralByIdQuery(long Id) : IQuery<GetCollateralByIdResult>;
