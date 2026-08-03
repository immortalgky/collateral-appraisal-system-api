namespace Parameter.Addresses.Models;

public class TitleProvince : ProvinceBase
{
    public ICollection<TitleDistrict> Districts { get; private set; } = [];
    private TitleProvince() { }

    public static TitleProvince Create(string code, string nameTh, string nameEn)
    {
        var province = new TitleProvince();
        province.Initialise(code, nameTh, nameEn);
        return province;
    }
}
