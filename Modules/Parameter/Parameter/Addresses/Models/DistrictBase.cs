namespace Parameter.Addresses.Models;

public abstract class DistrictBase
{
    public string Code { get; protected set; } = default!;
    public string NameTh { get; protected set; } = default!;
    public string NameEn { get; protected set; } = default!;
    public string ProvinceCode { get; protected set; } = default!;

    protected void Initialise(string code, string nameTh, string nameEn, string provinceCode)
    {
        Code = AddressRules.RequireCode(code, nameof(code), AddressRules.DistrictCodeLength);
        ProvinceCode = AddressRules.RequireCode(
            provinceCode, nameof(provinceCode), AddressRules.ProvinceCodeLength);
        Rename(nameTh, nameEn);
    }

    public void Rename(string nameTh, string nameEn)
    {
        NameTh = AddressRules.RequireName(nameTh, nameof(nameTh));
        NameEn = AddressRules.RequireName(nameEn, nameof(nameEn));
    }

    /// <summary>
    /// Reparenting is a real correction in the Title dataset, where provinces have historically
    /// been split and merged (e.g. the "(หนองคาย)บึงกาฬ" style entries).
    /// </summary>
    public void MoveToProvince(string provinceCode)
    {
        ProvinceCode = AddressRules.RequireCode(
            provinceCode, nameof(provinceCode), AddressRules.ProvinceCodeLength);
    }
}
