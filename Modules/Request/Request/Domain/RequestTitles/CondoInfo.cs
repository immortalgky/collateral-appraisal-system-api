namespace Request.Domain.RequestTitles;

public class CondoInfo : ValueObject
{
    public string? CondoName { get; }
    public string? BuildingNo { get; }
    public string? RoomNo { get; }
    public string? FloorNo { get; }
    public decimal? UsableArea { get; }

    private CondoInfo(
        string? condoName,
        string? buildingNo,
        string? roomNo,
        string? floorNo,
        decimal? usableArea
    )
    {
        CondoName = condoName;
        BuildingNo = buildingNo;
        RoomNo = roomNo;
        FloorNo = floorNo;
        UsableArea = usableArea;
    }

    public static CondoInfo Create(
        string? condoName,
        string? buildingNo,
        string? roomNo,
        string? floorNo,
        decimal? usableArea = null
    )
    {
        return new CondoInfo(
            condoName,
            buildingNo,
            roomNo,
            floorNo,
            usableArea
        );
    }

    public void Validate()
    {
        var ruleCheck = new RuleCheck();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(CondoName), "condoName is required.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(BuildingNo), "buildingNo is required.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(RoomNo), "roomNo is required.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(FloorNo), "floorNo is required.");
        ruleCheck.AddErrorIf(UsableArea is null || UsableArea < 0, "usableArea must be >= 0.");
        ruleCheck.ThrowIfInvalid();
    }
}

