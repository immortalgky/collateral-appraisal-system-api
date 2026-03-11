namespace Parameter.Addresses.Models;

public class DopaProvince : ProvinceBase
{
    public ICollection<DopaDistrict> Districts { get; private set; } = [];
    private DopaProvince() { }
}
