namespace Parameter.Addresses.Models;

public class DopaDistrict : DistrictBase
{
    public DopaProvince Province { get; private set; } = default!;
    public ICollection<DopaSubDistrict> SubDistricts { get; private set; } = [];
    private DopaDistrict() { }

    public static DopaDistrict Create(string code, string nameTh, string nameEn, string provinceCode)
    {
        var district = new DopaDistrict();
        district.Initialise(code, nameTh, nameEn, provinceCode);
        return district;
    }
}
