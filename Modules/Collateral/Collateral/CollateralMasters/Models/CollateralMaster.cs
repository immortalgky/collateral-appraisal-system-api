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

    private CollateralMaster() { }

    private CollateralMaster(CollateralType collateralType, long? hostCollateralId)
    {
        CollatType = collateralType;
        HostCollatId = hostCollateralId;
    }

    public static CollateralMaster Create(CollateralType collatType, long? hostCollatId)
    {
        return new CollateralMaster(collatType, hostCollatId);
    }

    public ICollateralModel? GetCollateral()
    {
        return CollatType switch
        {
            CollateralType.Land => CollateralLand,
            CollateralType.Building => CollateralBuilding,
            CollateralType.Condo => CollateralCondo,
            CollateralType.Machine => CollateralMachine,
            CollateralType.Vehicle => CollateralVehicle,
            CollateralType.Vessel => CollateralVessel,
            _ => null,
        };
    }

    public void SetCollateral(CollateralMaster collateralMaster)
    {
        switch (CollatType)
        {
            case CollateralType.Land:
                SetCollateralLand(collateralMaster.CollateralLand);
                break;
            case CollateralType.Building:
                SetCollateralBuilding(collateralMaster.CollateralBuilding);
                break;
            case CollateralType.Condo:
                SetCollateralCondo(collateralMaster.CollateralCondo);
                break;
            case CollateralType.Machine:
                SetCollateralMachine(collateralMaster.CollateralMachine);
                break;
            case CollateralType.Vehicle:
                SetCollateralVehicle(collateralMaster.CollateralVehicle);
                break;
            case CollateralType.Vessel:
                SetCollateralVessel(collateralMaster.CollateralVessel);
                break;
        }
    }

    public void SetCollateralLand(CollateralLand? collateralLand)
    {
        CollateralLand = collateralLand;
    }

    public void SetCollateralBuilding(CollateralBuilding? collateralBuilding)
    {
        CollateralBuilding = collateralBuilding;
    }

    public void SetCollateralCondo(CollateralCondo? collateralCondo)
    {
        CollateralCondo = collateralCondo;
    }

    public void SetCollateralMachine(CollateralMachine? collateralMachine)
    {
        CollateralMachine = collateralMachine;
    }

    public void SetCollateralVehicle(CollateralVehicle? collateralVehicle)
    {
        CollateralVehicle = collateralVehicle;
    }

    public void SetCollateralVessel(CollateralVessel? collateralVessel)
    {
        CollateralVessel = collateralVessel;
    }

    public void SetLandTitles(List<LandTitle> landTitles)
    {
        LandTitles = landTitles;
    }

    public void AddLandTitle(LandTitle landTitle)
    {
        LandTitles.Add(landTitle);
    }

    public void Update(CollateralMaster collateralMaster)
    {
        ValidateUpdate(collateralMaster);
        var collateral = GetCollateral();
        if (collateral is null)
        {
            SetCollateral(collateralMaster);
        }
        collateral!.Update(collateralMaster.GetCollateral());
        if (CollatType.Equals(CollateralType.Land))
        {
            UpdateLandTitles(collateralMaster.LandTitles);
        }
    }

    private void UpdateLandTitles(List<LandTitle> newLandTitles)
    {
        var comparer = EqualityComparer<LandTitle>.Create(
            (x, y) => x?.Id == y?.Id,
            landTitle => landTitle.Id.GetHashCode()
        );
        var landTitleDict = LandTitles.ToDictionary(landTitle => landTitle.Id);
        var newLandTitleSet = new HashSet<LandTitle>(newLandTitles, comparer);

        LandTitles.RemoveAll(landTitle => !newLandTitleSet.Contains(landTitle));
        foreach (var newLandTitle in newLandTitles)
        {
            if (landTitleDict.TryGetValue(newLandTitle.Id, out LandTitle? landTitle))
            {
                landTitle.Update(newLandTitle);
            }
            else
            {
                LandTitles.Add(newLandTitle);
            }
        }
    }

    private void ValidateUpdate(CollateralMaster collateralMaster)
    {
        RuleCheck
            .Valid()
            .AddErrorIf(
                !CollatType.Equals(collateralMaster.CollatType),
                "Cannot update collateral master with different type."
            )
            .ThrowIfInvalid();

        ValidateCollateral(collateralMaster.GetCollateral());
    }

    private static void ValidateCollateral(ICollateralModel? collateral)
    {
        RuleCheck
            .Valid()
            .AddErrorIf(collateral is null, "Cannot set collateral with null value.")
            .ThrowIfInvalid();
    }
}
