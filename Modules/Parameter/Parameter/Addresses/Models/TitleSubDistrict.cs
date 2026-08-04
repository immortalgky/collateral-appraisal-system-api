namespace Parameter.Addresses.Models;

public class TitleSubDistrict : SubDistrictBase
{
    public TitleDistrict District { get; private set; } = default!;
    private TitleSubDistrict() { }

    public static TitleSubDistrict Create(
        string code, string nameTh, string nameEn, string districtCode, string? postcode)
    {
        var subDistrict = new TitleSubDistrict();
        subDistrict.Initialise(code, nameTh, nameEn, districtCode, postcode);
        return subDistrict;
    }
}
