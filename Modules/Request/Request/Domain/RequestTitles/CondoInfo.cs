namespace Request.Domain.RequestTitles;

public class CondoInfo : ValueObject
{
    public string? CondoName { get; }
    public string? BuildingNumber { get; }
    public string? RoomNumber { get; }
    public string? FloorNumber { get; }
    public decimal? UsableArea { get; }

    private CondoInfo(
        string? condoName,
        string? buildingNumber,
        string? roomNumber,
        string? floorNumber,
        decimal? usableArea
    )
    {
        CondoName = condoName;
        BuildingNumber = buildingNumber;
        RoomNumber = roomNumber;
        FloorNumber = floorNumber;
        UsableArea = usableArea;
    }

    public static CondoInfo Create(
        string? condoName,
        string? buildingNumber,
        string? roomNumber,
        string? floorNumber,
        decimal? usableArea = null
    )
    {
        return new CondoInfo(
            condoName,
            buildingNumber,
            roomNumber,
            floorNumber,
            usableArea
        );
    }

    public void Validate()
    {
        var ruleCheck = new RuleCheck();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(CondoName), "condoName is required.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(BuildingNumber), "buildingNo is required.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(RoomNumber), "roomNo is required.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(FloorNumber), "floorNo is required.");
        ruleCheck.AddErrorIf(UsableArea is null || UsableArea < 0, "usableArea must be >= 0.");
        ruleCheck.ThrowIfInvalid();
    }
}