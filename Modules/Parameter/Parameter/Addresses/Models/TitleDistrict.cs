namespace Parameter.Addresses.Models;

public class TitleDistrict : DistrictBase
{
    public TitleProvince Province { get; private set; } = default!;
    public ICollection<TitleSubDistrict> SubDistricts { get; private set; } = [];
    private TitleDistrict() { }

    public static TitleDistrict Create(string code, string nameTh, string nameEn, string provinceCode)
    {
        var district = new TitleDistrict();
        district.Initialise(code, nameTh, nameEn, provinceCode);
        return district;
    }
}
