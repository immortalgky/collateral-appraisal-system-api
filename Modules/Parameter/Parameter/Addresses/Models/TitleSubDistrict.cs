namespace Parameter.Addresses.Models;

public class TitleSubDistrict : SubDistrictBase
{
    public TitleDistrict District { get; private set; } = default!;
    private TitleSubDistrict() { }
}
