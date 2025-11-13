namespace Request.Requests.ValueObjects;

public class CondoInfo : ValueObject
{
    public string? CondoName { get; } = default!;
    public string? BuildingNo { get; } = default!;
    public string? RoomNo { get; } = default!;
    public string? FloorNo { get; } = default!;
    private CondoInfo(
        string? condoName, 
        string? buildingNo, 
        string? roomNo, 
        string? floorNo
    )
    {
        CondoName = condoName;
        BuildingNo = buildingNo;
        RoomNo = roomNo;
        FloorNo = floorNo;
    }

    public static CondoInfo Create(
        string? condoName, 
        string? buildingNo, 
        string? roomNo, 
        string? floorNo
    )
    {
        return new CondoInfo(
            condoName, 
            buildingNo, 
            roomNo, 
            floorNo
        );
    }
}

