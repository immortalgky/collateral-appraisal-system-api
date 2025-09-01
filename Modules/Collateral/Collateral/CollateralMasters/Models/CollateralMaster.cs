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
        RuleCheck
            .Valid()
            .AddErrorIf(
                !CollatType.Equals(collateralMaster.CollatType),
                "Cannot update collateral master with different type."
            )
            .ThrowIfInvalid();

        switch (CollatType)
        {
            case CollateralType.Land:
                UpdateCollateralLand(collateralMaster.CollateralLand);
                UpdateLandTitles(collateralMaster.LandTitles);
                break;
            case CollateralType.Building:
                UpdateCollateralBuilding(collateralMaster.CollateralBuilding);
                break;
            case CollateralType.LandAndBuilding:
                UpdateCollateralLand(collateralMaster.CollateralLand);
                UpdateLandTitles(collateralMaster.LandTitles);
                UpdateCollateralBuilding(collateralMaster.CollateralBuilding);
                break;
            case CollateralType.Condo:
                UpdateCollateralCondo(collateralMaster.CollateralCondo);
                break;
            case CollateralType.Machine:
                UpdateCollateralMachine(collateralMaster.CollateralMachine);
                break;
            case CollateralType.Vehicle:
                UpdateCollateralVehicle(collateralMaster.CollateralVehicle);
                break;
            case CollateralType.Vessel:
                UpdateCollateralVessel(collateralMaster.CollateralVessel);
                break;
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

        // Even if the input has new land titles with duplicated id
        // All of them will still be added (because we don't use the hash set when adding)
        foreach (var newLandTitle in newLandTitles)
        {
            if (landTitleDict.TryGetValue(newLandTitle.Id, out LandTitle? landTitle))
            {
                landTitle.Update(newLandTitle);
            }
            else
            {
                newLandTitle.Id = 0;
                LandTitles.Add(newLandTitle);
            }
        }
    }

    private void UpdateCollateralLand(CollateralLand? collateralLand)
    {
        RuleCheck
            .Valid()
            .AddErrorIf(collateralLand is null, "Cannot set collateral land with null value.")
            .ThrowIfInvalid();

        if (CollateralLand is null)
        {
            SetCollateralLand(collateralLand);
        }
        else
        {
            CollateralLand.Update(collateralLand!);
        }
    }

    private void UpdateCollateralBuilding(CollateralBuilding? collateralBuilding)
    {
        RuleCheck
            .Valid()
            .AddErrorIf(
                collateralBuilding is null,
                "Cannot set collateral building with null value."
            )
            .ThrowIfInvalid();

        if (CollateralBuilding is null)
        {
            SetCollateralBuilding(collateralBuilding);
        }
        else
        {
            CollateralBuilding.Update(collateralBuilding!);
        }
    }

    private void UpdateCollateralCondo(CollateralCondo? collateralCondo)
    {
        RuleCheck
            .Valid()
            .AddErrorIf(collateralCondo is null, "Cannot set collateral condo with null value.")
            .ThrowIfInvalid();

        if (CollateralCondo is null)
        {
            SetCollateralCondo(collateralCondo);
        }
        else
        {
            CollateralCondo.Update(collateralCondo!);
        }
    }

    private void UpdateCollateralMachine(CollateralMachine? collateralMachine)
    {
        RuleCheck
            .Valid()
            .AddErrorIf(collateralMachine is null, "Cannot set collateral machine with null value.")
            .ThrowIfInvalid();

        if (CollateralMachine is null)
        {
            SetCollateralMachine(collateralMachine);
        }
        else
        {
            CollateralMachine.Update(collateralMachine!);
        }
    }

    private void UpdateCollateralVehicle(CollateralVehicle? collateralVehicle)
    {
        RuleCheck
            .Valid()
            .AddErrorIf(collateralVehicle is null, "Cannot set collateral vehicle with null value.")
            .ThrowIfInvalid();

        if (CollateralVehicle is null)
        {
            SetCollateralVehicle(collateralVehicle);
        }
        else
        {
            CollateralVehicle.Update(collateralVehicle!);
        }
    }

    private void UpdateCollateralVessel(CollateralVessel? collateralVessel)
    {
        RuleCheck
            .Valid()
            .AddErrorIf(collateralVessel is null, "Cannot set collateral vessel with null value.")
            .ThrowIfInvalid();

        if (CollateralVessel is null)
        {
            SetCollateralVessel(collateralVessel);
        }
        else
        {
            CollateralVessel.Update(collateralVessel!);
        }
    }
}
