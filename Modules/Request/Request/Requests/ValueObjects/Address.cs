namespace Request.Requests.ValueObjects;

public class Address : ValueObject
{
    public string? HouseNo { get; }
    public string? RoomNo { get; }
    public string? FloorNo { get; }
    public string? BuildingNo { get; }
    public string? ProjectName { get; }
    public string? Moo { get; }
    public string? Soi { get; }
    public string? Road { get; }
    public string? SubDistrict { get; }
    public string? District { get; }
    public string? Province { get; }
    public string? Postcode { get; }

    private Address()
    {
        // For EF Core
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private Address(string? houseNo, string? roomNo, string? floorNo, string? buildingNo, string? projectName,
        string? moo, string? soi, string? road, string? subDistrict, string? district, string? province,
        string? postcode)
    {
        HouseNo = houseNo;
        RoomNo = roomNo;
        FloorNo = floorNo;
        BuildingNo = buildingNo;
        ProjectName = projectName;
        Moo = moo;
        Soi = soi;
        Road = road;
        SubDistrict = subDistrict;
        District = district;
        Province = province;
        Postcode = postcode;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static Address Create(
        string? houseNo, string? roomNo, string? floorNo, string? buildingNo, string? projectName,
        string? moo, string? soi, string? road, string? subDistrict, string? district, string? province,
        string? postcode)
    {
        return new Address(houseNo, roomNo, floorNo, buildingNo, projectName,
            moo, soi, road, subDistrict, district, province,
            postcode);
    }
}