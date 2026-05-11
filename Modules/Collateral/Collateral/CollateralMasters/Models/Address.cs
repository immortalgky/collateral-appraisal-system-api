namespace Collateral.CollateralMasters.Models;

public class Address
{
    public string? Street { get; private set; }
    public string? Village { get; private set; }

    private Address() { }

    public Address(string? street, string? village)
    {
        Street = street;
        Village = village;
    }

    public void Update(string? street, string? village)
    {
        Street = street;
        Village = village;
    }
}
