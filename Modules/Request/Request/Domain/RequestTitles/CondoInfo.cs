namespace Request.Domain.RequestTitles;

public class CondoInfo : ValueObject
{
    public string? CondoName { get; }
    public string? BuildingNumber { get; }
    public string? CondoRegistrationNumber { get; }
    public string? RoomNumber { get; }
    public string? FloorNumber { get; }
    public decimal? UsableArea { get; }

    private CondoInfo(
        string? condoName,
        string? buildingNumber,
        string? condoRegistrationNumber,
        string? roomNumber,
        string? floorNumber,
        decimal? usableArea
    )
    {
        CondoName = condoName;
        BuildingNumber = buildingNumber;
        CondoRegistrationNumber = condoRegistrationNumber;
        RoomNumber = roomNumber;
        FloorNumber = floorNumber;
        UsableArea = usableArea;
    }

    public static CondoInfo Create(
        string? condoName,
        string? buildingNumber,
        string? condoRegistrationNumber,
        string? roomNumber,
        string? floorNumber,
        decimal? usableArea = null
    )
    {
        return new CondoInfo(
            condoName,
            buildingNumber,
            condoRegistrationNumber,
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
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(CondoRegistrationNumber), "condoRegistrationNo is required.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(RoomNumber), "roomNo is required.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(FloorNumber), "floorNo is required.");
        ruleCheck.AddErrorIf(UsableArea is null || UsableArea < 0, "usableArea must be >= 0.");
        ruleCheck.ThrowIfInvalid();
    }
}