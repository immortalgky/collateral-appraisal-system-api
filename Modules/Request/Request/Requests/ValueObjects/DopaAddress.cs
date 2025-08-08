namespace Request.Requests.ValueObjects;

public class DopaAddress : ValueObject
{
    private DopaAddress(string? dopaHouseNo, string? dopaRoomNo, string? dopaFloorNo, string? dopaBuildingNo, string? dopaMoo,
        string? dopaSoi, string? dopaRoad, string? dopaSubDistrict, string? dopaDistrict, string? dopaProvince,
        string? dopaPostcode)
    {
        DopaHouseNo = dopaHouseNo;
        DopaRoomNo = dopaRoomNo;
        DopaFloorNo = dopaFloorNo;
        DopaBuildingNo = dopaBuildingNo;
        DopaMoo = dopaMoo;
        DopaSoi = dopaSoi;
        DopaRoad = dopaRoad;
        DopaSubDistrict = dopaSubDistrict;
        DopaDistrict = dopaDistrict;
        DopaProvince = dopaProvince;
        DopaPostcode = dopaPostcode;
    }

    public string? DopaHouseNo { get; }
    public string? DopaRoomNo { get; }
    public string? DopaFloorNo { get; }
    public string? DopaBuildingNo { get; }
    public string? DopaMoo { get; }
    public string? DopaSoi { get; }
    public string? DopaRoad { get; }
    public string? DopaSubDistrict { get; }
    public string? DopaDistrict { get; }
    public string? DopaProvince { get; }
    public string? DopaPostcode { get; }

    public static DopaAddress Create(
        string? dopaHouseNo, string? dopaRoomNo, string? dopaFloorNo, string? dopaBuildingNo, string? dopaMoo,
        string? dopaSoi, string? dopaRoad, string? dopaSubDistrict, string? dopaDistrict, string? dopaProvince,
        string? dopaPostcode)
    {
        return new DopaAddress(dopaHouseNo, dopaRoomNo, dopaFloorNo, dopaBuildingNo,
            dopaMoo, dopaSoi, dopaRoad, dopaSubDistrict, dopaDistrict, dopaProvince,
            dopaPostcode);
    }
}