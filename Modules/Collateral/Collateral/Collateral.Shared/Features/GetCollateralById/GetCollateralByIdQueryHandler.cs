namespace Collateral.Collateral.Shared.Features.GetCollateralById;

public class GetCollateralByIdQueryHandler(ICollateralService collateralService)
    : IQueryHandler<GetCollateralByIdQuery, GetCollateralByIdResult>
{
    public async Task<GetCollateralByIdResult> Handle(
        GetCollateralByIdQuery query,
        CancellationToken cancellationToken
    )
    {
        var collateral = await collateralService.GetCollateralById(query.Id, cancellationToken);
        return ConvertModelToDto(collateral);
    }

    private static GetCollateralByIdResult ConvertModelToDto(CollateralMaster collateral)
    {
        return new GetCollateralByIdResult(
            collateral.Id,
            collateral.CollatType.ToString(),
            collateral.HostCollatId,
            collateral.CollateralMachine?.ToDto(),
            collateral.CollateralVehicle?.ToDto(),
            collateral.CollateralVessel?.ToDto(),
            collateral.CollateralLand?.ToDto(),
            collateral.CollateralBuilding?.ToDto(),
            collateral.CollateralCondo?.ToDto(),
            [.. collateral.LandTitles.Select(domain => domain.ToDto())]
        );
    }
}
