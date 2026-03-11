namespace Parameter.Addresses.Models;

public abstract class SubDistrictBase
{
    public string Code { get; private set; } = default!;
    public string NameTh { get; private set; } = default!;
    public string NameEn { get; private set; } = default!;
    public string DistrictCode { get; private set; } = default!;
    public string Postcode { get; private set; } = default!;
}
