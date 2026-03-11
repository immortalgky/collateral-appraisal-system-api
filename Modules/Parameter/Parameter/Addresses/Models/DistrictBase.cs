namespace Parameter.Addresses.Models;

public abstract class DistrictBase
{
    public string Code { get; private set; } = default!;
    public string NameTh { get; private set; } = default!;
    public string NameEn { get; private set; } = default!;
    public string ProvinceCode { get; private set; } = default!;
}
