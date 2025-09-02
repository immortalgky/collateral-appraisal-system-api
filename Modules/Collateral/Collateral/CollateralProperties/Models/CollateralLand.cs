using Collateral.CollateralProperties.ValueObjects;
using Shared.Dtos;

namespace Collateral.CollateralProperties.Models;

public class CollateralLand : Entity<long>
{
    public long CollatId { get; private set; }
    public Coordinate Coordinate { get; private set; } = default!;
    public CollateralLocation CollateralLocation { get; private set; } = default!;
    public string LandDesc { get; private set; } = default!;

    private CollateralLand() { }

    private CollateralLand(
        long collatId,
        Coordinate coordinate,
        CollateralLocation collateralLocation,
        string landDesc
    )
    {
        CollatId = collatId;
        Coordinate = coordinate;
        CollateralLocation = collateralLocation;
        LandDesc = landDesc;
    }

    public static CollateralLand Create(
        long collatId,
        Coordinate coordinate,
        CollateralLocation collateralLocation,
        string landDesc
    )
    {
        return new CollateralLand(collatId, coordinate, collateralLocation, landDesc);
    }

    public static CollateralLand FromRequestTitleDto(long collatId, RequestTitleDto requestTitleDto)
    {
        return new CollateralLand(
            collatId,
            Coordinate.Create(0, 0),
            CollateralLocation.Create(
                requestTitleDto.TitleAddress.SubDistrict ?? "",
                requestTitleDto.TitleAddress.District ?? "",
                requestTitleDto.TitleAddress.Province ?? "",
                ""
            ),
            ""
        );
    }
}
