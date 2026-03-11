namespace Parameter.Addresses.Models;

public class TitleDistrict : DistrictBase
{
    public TitleProvince Province { get; private set; } = default!;
    public ICollection<TitleSubDistrict> SubDistricts { get; private set; } = [];
    private TitleDistrict() { }
}
