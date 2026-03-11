namespace Parameter.Addresses.Models;

public class DopaSubDistrict : SubDistrictBase
{
    public DopaDistrict District { get; private set; } = default!;
    private DopaSubDistrict() { }
}
