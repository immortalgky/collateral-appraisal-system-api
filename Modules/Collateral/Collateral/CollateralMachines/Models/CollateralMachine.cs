namespace Collateral.CollateralMachines.Models;

public class CollateralMachine : Aggregate<long>
{
    public long CollatId { get; private set; } = default!;
    public CollateralProperty CollateralMachineProperty { get; private set; } = default!;
    public CollateralDetail CollateralMachineDetail { get; private set; } = default!;
    public CollateralSize CollateralMachineSize { get; private set; } = default!;
    public string ChassisNo { get; private set; } = default!;

    private CollateralMachine() { }

    private CollateralMachine(
        long collatId,
        CollateralProperty collateralMachineProperty,
        CollateralDetail collateralMachineDetail,
        CollateralSize collateralMachineSize,
        string chassisNo
    )
    {
        CollatId = collatId;
        CollateralMachineProperty = collateralMachineProperty;
        CollateralMachineDetail = collateralMachineDetail;
        CollateralMachineSize = collateralMachineSize;
        ChassisNo = chassisNo;
    }

    public static CollateralMachine Create(
        long collatId,
        CollateralProperty collateralMachineProperty,
        CollateralDetail collateralMachineDetail,
        CollateralSize collateralMachineSize,
        string chassisNo
    )
    {
        return new CollateralMachine(
            collatId,
            collateralMachineProperty,
            collateralMachineDetail,
            collateralMachineSize,
            chassisNo
        );
    }

    public void Update(CollateralMachine collateralMachine)
    {
        if (!CollateralMachineProperty.Equals(collateralMachine.CollateralMachineProperty))
        {
            CollateralMachineProperty = collateralMachine.CollateralMachineProperty;
        }

        if (!CollateralMachineDetail.Equals(collateralMachine.CollateralMachineDetail))
        {
            CollateralMachineDetail = collateralMachine.CollateralMachineDetail;
        }

        if (!CollateralMachineSize.Equals(collateralMachine.CollateralMachineSize))
        {
            CollateralMachineSize = collateralMachine.CollateralMachineSize;
        }

        if (!ChassisNo.Equals(collateralMachine.ChassisNo))
        {
            ChassisNo = collateralMachine.ChassisNo;
        }
    }
}
