namespace Collateral.CollateralVessels.Models;

public class CollateralVessel : Aggregate<long>
{
    public long CollatId { get; private set; } = default!;
    public long ApprId { get; private set; } = default!;
    public CollateralProperty CollateralVesselProperty { get; private set; } = default!;
    public CollateralDetail CollateralVesselDetail { get; private set; } = default!;
    public CollateralSize CollateralVesselSize { get; private set; } = default!;

    private CollateralVessel() { }

    private CollateralVessel(
        long collatId,
        long apprId,
        CollateralProperty collateralVesselProperty,
        CollateralDetail collateralVesselDetail,
        CollateralSize collateralVesselSize
    )
    {
        CollatId = collatId;
        ApprId = apprId;
        CollateralVesselProperty = collateralVesselProperty;
        CollateralVesselDetail = collateralVesselDetail;
        CollateralVesselSize = collateralVesselSize;
    }
    public static CollateralVessel Create(
        long collatId,
        long apprId,
        CollateralProperty collateralVesselProperty,
        CollateralDetail collateralVesselDetail,
        CollateralSize collateralVesselSize
    )
    {
        return new CollateralVessel(
            collatId,
            apprId,
            collateralVesselProperty,
            collateralVesselDetail,
            collateralVesselSize
        );
    }
}