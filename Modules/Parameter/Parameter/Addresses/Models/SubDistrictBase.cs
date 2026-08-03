namespace Parameter.Addresses.Models;

public abstract class SubDistrictBase
{
    public string Code { get; protected set; } = default!;
    public string NameTh { get; protected set; } = default!;
    public string NameEn { get; protected set; } = default!;
    public string DistrictCode { get; protected set; } = default!;
    public string? Postcode { get; protected set; }

    protected void Initialise(
        string code, string nameTh, string nameEn, string districtCode, string? postcode)
    {
        Code = AddressRules.RequireCode(code, nameof(code), AddressRules.SubDistrictCodeLength);
        DistrictCode = AddressRules.RequireCode(
            districtCode, nameof(districtCode), AddressRules.DistrictCodeLength);
        Update(nameTh, nameEn, postcode);
    }

    public void Update(string nameTh, string nameEn, string? postcode)
    {
        NameTh = AddressRules.RequireName(nameTh, nameof(nameTh));
        NameEn = AddressRules.RequireName(nameEn, nameof(nameEn));
        Postcode = AddressRules.NormalisePostcode(postcode);
    }

    public void MoveToDistrict(string districtCode)
    {
        DistrictCode = AddressRules.RequireCode(
            districtCode, nameof(districtCode), AddressRules.DistrictCodeLength);
    }
}
