namespace Collateral.Application.Features.CollateralMasters.GetById;

public record GetCollateralMasterByIdQuery(Guid Id) : IQuery<GetCollateralMasterByIdResult>;
