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
}