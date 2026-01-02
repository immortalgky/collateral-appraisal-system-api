namespace Request.Domain.Requests;

public class Address : ValueObject
{
    public string? HouseNumber { get; }
    public string? ProjectName { get; }
    public string? Moo { get; }
    public string? Soi { get; }
    public string? Road { get; }
    public string? SubDistrict { get; }
    public string? District { get; }
    public string? Province { get; }
    public string? Postcode { get; }

    private Address()
    {
        // For EF Core
    }

    private Address(AddressData data)
    {
        HouseNumber = data.HouseNumber;
        ProjectName = data.ProjectName;
        Moo = data.Moo;
        Soi = data.Soi;
        Road = data.Road;
        SubDistrict = data.SubDistrict;
        District = data.District;
        Province = data.Province;
        Postcode = data.Postcode;
    }

    public static Address Create(AddressData data)
    {
        return new Address(data);
    }

    public void Validate()
    {
        var ruleCheck = new RuleCheck();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(SubDistrict), "subDistrict is required.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(District), "district is required.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(Province), "province is required.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(Postcode), "postcode is required.");
        ruleCheck.ThrowIfInvalid();
    }
}

public record AddressData(
    string? HouseNumber,
    string? ProjectName,
    string? Moo,
    string? Soi,
    string? Road,
    string? SubDistrict,
    string? District,
    string? Province,
    string? Postcode
);