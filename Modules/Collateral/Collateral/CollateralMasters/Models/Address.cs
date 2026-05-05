namespace Collateral.CollateralMasters.Models;

public class Address
{
    public string? Street { get; private set; }
    public string? Village { get; private set; }
    public string? PostalCode { get; private set; }

    private Address() { }

    public Address(string? street, string? village, string? postalCode)
    {
        Street = street;
        Village = village;
        PostalCode = postalCode;
    }

    public void Update(string? street, string? village, string? postalCode)
    {
        Street = street;
        Village = village;
        PostalCode = postalCode;
    }
}
