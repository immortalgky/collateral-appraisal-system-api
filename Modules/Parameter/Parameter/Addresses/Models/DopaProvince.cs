namespace Parameter.Addresses.Models;

public class DopaProvince : ProvinceBase
{
    public ICollection<DopaDistrict> Districts { get; private set; } = [];
    private DopaProvince() { }

    public static DopaProvince Create(string code, string nameTh, string nameEn)
    {
        var province = new DopaProvince();
        province.Initialise(code, nameTh, nameEn);
        return province;
    }
}
