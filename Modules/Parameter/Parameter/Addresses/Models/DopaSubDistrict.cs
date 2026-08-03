namespace Parameter.Addresses.Models;

public class DopaSubDistrict : SubDistrictBase
{
    public DopaDistrict District { get; private set; } = default!;
    private DopaSubDistrict() { }

    public static DopaSubDistrict Create(
        string code, string nameTh, string nameEn, string districtCode, string? postcode)
    {
        var subDistrict = new DopaSubDistrict();
        subDistrict.Initialise(code, nameTh, nameEn, districtCode, postcode);
        return subDistrict;
    }
}
