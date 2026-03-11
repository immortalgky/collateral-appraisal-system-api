namespace Parameter.Addresses.Models;

public class TitleProvince : ProvinceBase
{
    public ICollection<TitleDistrict> Districts { get; private set; } = [];
    private TitleProvince() { }
}
