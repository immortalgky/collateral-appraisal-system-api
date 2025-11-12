namespace Request.Requests.ValueObjects;

public class Condo : ValueObject
{
    public string? CondoName { get; } = default!;
    public string? BuildingNo { get; } = default!;
    public string? RoomNo { get; } = default!;
    public string? FloorNo { get; } = default!;
    private Condo(
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

    public static Condo Create(
        string? condoName, 
        string? buildingNo, 
        string? roomNo, 
        string? floorNo
    )
    {
        return new Condo(
            condoName, 
            buildingNo, 
            roomNo, 
            floorNo
        );
    }
}

