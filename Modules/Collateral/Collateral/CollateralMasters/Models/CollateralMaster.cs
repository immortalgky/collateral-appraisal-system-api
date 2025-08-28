using Collateral.CollateralMasters.ValueObjects;

namespace Collateral.CollateralMasters.Models;

public class CollateralMaster : Aggregate<long>
{
    public CollateralType CollatType { get; private set; } = default!;
    public long? HostCollatId { get; private set; } = default!;
    public CollateralMachine? CollateralMachine { get; private set; }
    public CollateralVehicle? CollateralVehicle { get; private set; }
    public CollateralVessel? CollateralVessel { get; private set; }

    public CollateralLand? CollateralLand { get; private set; }
    public CollateralBuilding? CollateralBuilding { get; private set; }
    public CollateralCondo? CollateralCondo { get; private set; }
    public List<LandTitle> LandTitles { get; private set; } = [];

    private CollateralMaster()
    {
    }

    private CollateralMaster(CollateralType collateralType, long? hostCollateralId)
    {
        CollatType = collateralType;
        HostCollatId = hostCollateralId;
    }

    public static CollateralMaster Create(CollateralType collatType, long? hostCollatId)
    {
        return new CollateralMaster(
            collatType,
            hostCollatId
        );
    }

    public void SetCollateralLand(CollateralLand collateralLand)
    {
        CollateralLand = collateralLand;
    }

    public void SetCollateralBuilding(CollateralBuilding collateralBuilding)
    {
        CollateralBuilding = collateralBuilding;
    }

    public void SetCollateralCondo(CollateralCondo collateralCondo)
    {
        CollateralCondo = collateralCondo;
    }

    public void SetCollateralMachine(CollateralMachine collateralMachine)
    {
        CollateralMachine = collateralMachine;
    }

    public void SetCollateralVehicle(CollateralVehicle collateralVehicle)
    {
        CollateralVehicle = collateralVehicle;
    }

    public void SetCollateralVessel(CollateralVessel collateralVessel)
    {
        CollateralVessel = collateralVessel;
    }

    public void SetLandTitle(List<LandTitle> landTitles)
    {
        LandTitles = landTitles;
    }

    public void AddLandTitle(LandTitle landTitle)
    {
        LandTitles.Add(landTitle);
    }
}