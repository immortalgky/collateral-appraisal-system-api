namespace Parameter.Addresses.Models;

public class DopaDistrict : DistrictBase
{
    public DopaProvince Province { get; private set; } = default!;
    public ICollection<DopaSubDistrict> SubDistricts { get; private set; } = [];
    private DopaDistrict() { }
}
