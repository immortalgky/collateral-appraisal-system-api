namespace Collateral.CollateralVessels.Models;

public class CollateralVessel : Aggregate<long>, ICollateralModel
{
    public long CollatId { get; private set; } = default!;
    public CollateralProperty CollateralVesselProperty { get; private set; } = default!;
    public CollateralDetail CollateralVesselDetail { get; private set; } = default!;
    public CollateralSize CollateralVesselSize { get; private set; } = default!;

    private CollateralVessel() { }

    private CollateralVessel(
        long collatId,
        CollateralProperty collateralVesselProperty,
        CollateralDetail collateralVesselDetail,
        CollateralSize collateralVesselSize
    )
    {
        CollatId = collatId;
        CollateralVesselProperty = collateralVesselProperty;
        CollateralVesselDetail = collateralVesselDetail;
        CollateralVesselSize = collateralVesselSize;
    }

    public static CollateralVessel Create(
        long collatId,
        CollateralProperty collateralVesselProperty,
        CollateralDetail collateralVesselDetail,
        CollateralSize collateralVesselSize
    )
    {
        return new CollateralVessel(
            collatId,
            collateralVesselProperty,
            collateralVesselDetail,
            collateralVesselSize
        );
    }

    public void Update(ICollateralModel? collateral)
    {
        if (collateral is CollateralVessel collateralVessel)
        {
            Update(collateralVessel);
        }
    }

    public void Update(CollateralVessel collateralVessel)
    {
        if (!CollateralVesselProperty.Equals(collateralVessel.CollateralVesselProperty))
        {
            CollateralVesselProperty = collateralVessel.CollateralVesselProperty;
        }

        if (!CollateralVesselDetail.Equals(collateralVessel.CollateralVesselDetail))
        {
            CollateralVesselDetail = collateralVessel.CollateralVesselDetail;
        }

        if (!CollateralVesselSize.Equals(collateralVessel.CollateralVesselSize))
        {
            CollateralVesselSize = collateralVessel.CollateralVesselSize;
        }
    }
}
