namespace Collateral.CollateralVehicles.Models;

public class CollateralVehicle : Aggregate<long>, ICollateralModel
{
    public long CollatId { get; private set; } = default!;
    public CollateralProperty CollateralVehicleProperty { get; private set; } = default!;
    public CollateralDetail CollateralVehicleDetail { get; private set; } = default!;
    public CollateralSize CollateralVehicleSize { get; private set; } = default!;
    public string ChassisNo { get; private set; } = default!;

    private CollateralVehicle() { }

    private CollateralVehicle(
        long collatId,
        CollateralProperty collateralVehicleProperty,
        CollateralDetail collateralVehicleDetail,
        CollateralSize collateralVehicleSize,
        string chassisNo
    )
    {
        CollatId = collatId;
        CollateralVehicleProperty = collateralVehicleProperty;
        CollateralVehicleDetail = collateralVehicleDetail;
        CollateralVehicleSize = collateralVehicleSize;
        ChassisNo = chassisNo;
    }

    public static CollateralVehicle Create(
        long collatId,
        CollateralProperty collateralVehicleProperty,
        CollateralDetail collateralVehicleDetail,
        CollateralSize collateralVehicleSize,
        string chassisNo
    )
    {
        return new CollateralVehicle(
            collatId,
            collateralVehicleProperty,
            collateralVehicleDetail,
            collateralVehicleSize,
            chassisNo
        );
    }

    public void Update(ICollateralModel? collateral)
    {
        if (collateral is CollateralVehicle collateralVehicle)
        {
            Update(collateralVehicle);
        }
    }

    public void Update(CollateralVehicle collateralVehicle)
    {
        if (!CollateralVehicleProperty.Equals(collateralVehicle.CollateralVehicleProperty))
        {
            CollateralVehicleProperty = collateralVehicle.CollateralVehicleProperty;
        }

        if (!CollateralVehicleDetail.Equals(collateralVehicle.CollateralVehicleDetail))
        {
            CollateralVehicleDetail = collateralVehicle.CollateralVehicleDetail;
        }

        if (!CollateralVehicleSize.Equals(collateralVehicle.CollateralVehicleSize))
        {
            CollateralVehicleSize = collateralVehicle.CollateralVehicleSize;
        }

        if (!ChassisNo.Equals(collateralVehicle.ChassisNo))
        {
            ChassisNo = collateralVehicle.ChassisNo;
        }
    }
}
