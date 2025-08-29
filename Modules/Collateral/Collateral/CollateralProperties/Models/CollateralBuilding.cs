namespace Collateral.CollateralProperties.Models;

public class CollateralBuilding : Entity<long>, ICollateralModel
{
    public long CollatId { get; private set; }
    public string BuildingNo { get; private set; } = default!;
    public string ModelName { get; private set; } = default!;
    public string HouseNo { get; private set; } = default!;
    public string BuiltOnTitleNo { get; private set; } = default!;
    public string Owner { get; private set; } = default!;

    private CollateralBuilding() { }

    private CollateralBuilding(
        long collatId,
        string buildingNo,
        string modelName,
        string houseNo,
        string builtOnTitleNo,
        string owner
    )
    {
        CollatId = collatId;
        BuildingNo = buildingNo;
        ModelName = modelName;
        HouseNo = houseNo;
        BuiltOnTitleNo = builtOnTitleNo;
        Owner = owner;
    }

    public static CollateralBuilding Create(
        long collatId,
        string buildingNo,
        string modelName,
        string houseNo,
        string builtOnTitleNo,
        string owner
    )
    {
        return new CollateralBuilding(
            collatId,
            buildingNo,
            modelName,
            houseNo,
            builtOnTitleNo,
            owner
        );
    }

    public void Update(ICollateralModel? collateral)
    {
        if (collateral is CollateralBuilding collateralBuilding)
        {
            Update(collateralBuilding);
        }
    }

    public void Update(CollateralBuilding collateralBuilding)
    {
        if (!BuildingNo.Equals(collateralBuilding.BuildingNo))
        {
            BuildingNo = collateralBuilding.BuildingNo;
        }

        if (!ModelName.Equals(collateralBuilding.ModelName))
        {
            ModelName = collateralBuilding.ModelName;
        }

        if (!HouseNo.Equals(collateralBuilding.HouseNo))
        {
            HouseNo = collateralBuilding.HouseNo;
        }

        if (!BuiltOnTitleNo.Equals(collateralBuilding.BuiltOnTitleNo))
        {
            BuiltOnTitleNo = collateralBuilding.BuiltOnTitleNo;
        }

        if (!Owner.Equals(collateralBuilding.Owner))
        {
            Owner = collateralBuilding.Owner;
        }
    }
}
