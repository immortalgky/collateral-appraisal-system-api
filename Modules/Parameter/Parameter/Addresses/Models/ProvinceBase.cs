namespace Parameter.Addresses.Models;

public abstract class ProvinceBase
{
    public string Code { get; protected set; } = default!;
    public string NameTh { get; protected set; } = default!;
    public string NameEn { get; protected set; } = default!;

    /// <summary>
    /// Code is the natural key and is referenced by districts (and by collateral rows, which store
    /// the geocode rather than the name), so it is set once at creation and never edited.
    /// </summary>
    protected void Initialise(string code, string nameTh, string nameEn)
    {
        Code = AddressRules.RequireCode(code, nameof(code), AddressRules.ProvinceCodeLength);
        Rename(nameTh, nameEn);
    }

    public void Rename(string nameTh, string nameEn)
    {
        NameTh = AddressRules.RequireName(nameTh, nameof(nameTh));
        NameEn = AddressRules.RequireName(nameEn, nameof(nameEn));
    }
}

/// <summary>
/// Column widths from the address tables, enforced in the domain so a bad value fails with a
/// readable message instead of a SQL Server truncation error. Codes are NOT numeric: the Title
/// (Land Department) dataset uses values such as "A0"/"A1"/"A2".
/// </summary>
public static class AddressRules
{
    public const int ProvinceCodeLength = 2;
    public const int DistrictCodeLength = 4;
    public const int SubDistrictCodeLength = 6;
    public const int PostcodeLength = 5;
    public const int NameMaxLength = 150;

    public static string RequireCode(string? code, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", paramName);

        var trimmed = code.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"Code cannot exceed {maxLength} characters.", paramName);

        return trimmed;
    }

    public static string RequireName(string? name, string paramName)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", paramName);

        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
            throw new ArgumentException(
                $"Name cannot exceed {NameMaxLength} characters.", paramName);

        return trimmed;
    }

    /// <summary>Postcode is optional — the Title dataset leaves it null for historical entries.</summary>
    public static string? NormalisePostcode(string? postcode)
    {
        if (string.IsNullOrWhiteSpace(postcode)) return null;

        var trimmed = postcode.Trim();
        if (trimmed.Length > PostcodeLength)
            throw new ArgumentException(
                $"Postcode cannot exceed {PostcodeLength} characters.", nameof(postcode));

        return trimmed;
    }
}
