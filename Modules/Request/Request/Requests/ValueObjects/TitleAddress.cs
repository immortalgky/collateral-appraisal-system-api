namespace Request.Requests.ValueObjects;

public class TitleAddress : ValueObject
{
    private TitleAddress(string? houseNo, string? roomNo, string? floorNo, string? buildingNo,
        string? moo, string? soi, string? road, string subDistrict, string district,
        string province, string? postcode)
    {
        HouseNo = houseNo;
        RoomNo = roomNo;
        FloorNo = floorNo;
        BuildingNo = buildingNo;
        Moo = moo;
        Soi = soi;
        Road = road;
        SubDistrict = subDistrict;
        District = district;
        Province = province;
        Postcode = postcode;
    }

    public string? HouseNo { get; }
    public string? RoomNo { get; }
    public string? FloorNo { get; }
    public string? BuildingNo { get; }
    public string? Moo { get; }
    public string? Soi { get; }
    public string? Road { get; }
    public string SubDistrict { get; }
    public string District { get; }
    public string Province { get; }
    public string? Postcode { get; }

    public static TitleAddress Create(
        string? houseNo, string? roomNo, string? floorNo, string? buildingNo,
        string? moo, string? soi, string? road, string subDistrict, string district, string province,
        string? postcode)
    {
        return new TitleAddress(houseNo, roomNo, floorNo, buildingNo,
            moo, soi, road, subDistrict, district, province,
            postcode);
    }
}