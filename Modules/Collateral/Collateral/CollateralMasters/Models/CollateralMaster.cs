using Collateral.CollateralProperties.Models;

namespace Collateral.CollateralMasters.Models;

public class CollateralMaster : Aggregate<long>
{
    public string CollatType { get; private set; } = default!;
    public long? HostCollatId { get; private set; } = default!;
    public CollateralMachine? CollateralMachine { get; private set; }
    public CollateralVehicle? CollateralVehicle { get; private set; }
    public CollateralVessel? CollateralVessel { get; private set; }

    public CollateralLand? CollateralLand { get; private set; }
    public CollateralBuilding? CollateralBuilding { get; private set; }
    public CollateralCondo? CollateralCondo { get; private set; }
    public LandTitle? LandTitle { get; private set; }

    private CollateralMaster()
    {
    }

    private CollateralMaster(string collateralType, long? hostCollateralId)
    {
        ArgumentNullException.ThrowIfNull(collateralType);

        CollatType = collateralType;
        HostCollatId = hostCollateralId;
    }

    public static CollateralMaster Create(string collatType, long? hostCollatId)
    {
        ArgumentNullException.ThrowIfNull(collatType);

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

    public void SetLandTitle(LandTitle landTitle)
    {
        LandTitle = landTitle;
    }
}