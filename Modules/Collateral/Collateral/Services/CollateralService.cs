namespace Collateral.Services;

public class CollateralService(
    ICollateralRepository collateralRepository
) : ICollateralService
{
    public async Task<CollateralMaster> CreateCollateral(
        CollateralType collatType,
        CollateralDto collateral,
        CancellationToken cancellationToken = default
    )
    {
        var collateralMaster = CollateralMaster.Create(collatType, null);
        await collateralRepository.CreateCollateralMasterAsync(collateralMaster, cancellationToken);

        switch (collatType)
        {
            case CollateralType.Land:
                var collateralLand = collateral.CollateralLand!.ToDomain(collateralMaster.Id);

                var landTitles = collateral
                    .LandTitles!.Select(dto => dto.ToDomain(collateralMaster.Id))
                    .ToList();
                collateralMaster.SetCollateralLand(collateralLand);
                collateralMaster.SetLandTitle(landTitles);
                break;
            case CollateralType.Building:
                var collateralBuilding = collateral.CollateralBuilding!.ToDomain(
                    collateralMaster.Id
                );
                collateralMaster.SetCollateralBuilding(collateralBuilding);
                break;
            case CollateralType.Condo:
                var collateralCondo = collateral.CollateralCondo!.ToDomain(collateralMaster.Id);
                collateralMaster.SetCollateralCondo(collateralCondo);
                break;
            case CollateralType.Machine:
                var collateralMachine = collateral.CollateralMachine!.ToDomain(collateralMaster.Id);
                collateralMaster.SetCollateralMachine(collateralMachine);
                break;
            case CollateralType.Vehicle:
                var collateralVehicle = collateral.CollateralVehicle!.ToDomain(collateralMaster.Id);
                collateralMaster.SetCollateralVehicle(collateralVehicle);
                break;
            case CollateralType.Vessel:
                var collateralVessel = collateral.CollateralVessel!.ToDomain(collateralMaster.Id);
                collateralMaster.SetCollateralVessel(collateralVessel);
                break;
        }
        await collateralRepository.SaveChangesAsync(cancellationToken);

        return collateralMaster;
    }
}
