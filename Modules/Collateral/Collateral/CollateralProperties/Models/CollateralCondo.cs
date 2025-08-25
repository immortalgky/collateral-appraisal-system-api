using Collateral.CollateralProperties.ValueObjects;

namespace Collateral.CollateralProperties.Models;

public class CollateralCondo : Entity<long>
{
    public long CollatId { get; private set; }
    public string CondoName { get; private set; } = default!;
    public string BuildingNo { get; private set; } = default!;
    public string ModelName { get; private set; } = default!;
    public string BuiltOnTitleNo { get; private set; } = default!;
    public string CondoRegisNo { get; private set; } = default!;
    public string RoomNo { get; private set; } = default!;
    public int FloorNo { get; private set; }
    public decimal UsableArea { get; private set; }
    public CollateralLocation CollateralLocation { get; private set; } = default!;
    public Coordinate Coordinate { get; private set; } = default!;
    public string Owner { get; private set; } = default!;

    private CollateralCondo() { }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private CollateralCondo(
        long collatId,
        string condoName,
        string buildingNo,
        string modelName,
        string builtOnTitleNo,
        string condoRegisNo,
        string roomNo,
        int floorNo,
        decimal usableArea,
        CollateralLocation collateralLocation,
        Coordinate coordinate,
        string owner
    )
    {
        CollatId = collatId;
        CondoName = condoName;
        BuildingNo = buildingNo;
        ModelName = modelName;
        BuiltOnTitleNo = builtOnTitleNo;
        CondoRegisNo = condoRegisNo;
        RoomNo = roomNo;
        FloorNo = floorNo;
        UsableArea = usableArea;
        CollateralLocation = collateralLocation;
        Coordinate = coordinate;
        Owner = owner;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static CollateralCondo Create(
        long collatId,
        string condoName,
        string buildingNo,
        string modelName,
        string builtOnTitleNo,
        string condoRegisNo,
        string roomNo,
        int floorNo,
        decimal usableArea,
        CollateralLocation collateralLocation,
        Coordinate coordinate,
        string owner
    )
    {
        return new CollateralCondo(
            collatId,
            condoName,
            buildingNo,
            modelName,
            builtOnTitleNo,
            condoRegisNo,
            roomNo,
            floorNo,
            usableArea,
            collateralLocation,
            coordinate,
            owner
        );
    }
}
