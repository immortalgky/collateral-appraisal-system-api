namespace Collateral.Collateral.Shared.ValueObjects;

public class CollateralDetail : ValueObject
{
    public string? EngineNo { get; }
    public string? RegistrationNo { get; }
    public int? YearOfManufacture { get; }
    public string? CountryOfManufacture { get; }
    public DateTime? PurchaseDate { get; }
    public decimal? PurchasePrice { get; }

    private CollateralDetail() { }
    private CollateralDetail(
        string? engineNo,
        string? registrationNo,
        int? yearOfManufacture,
        string? countryOfManufacture,
        DateTime? purchaseDate,
        decimal? purchasePrice
    )
    {
        EngineNo = engineNo;
        RegistrationNo = registrationNo;
        YearOfManufacture = yearOfManufacture;
        CountryOfManufacture = countryOfManufacture;
        PurchaseDate = purchaseDate;
        PurchasePrice = purchasePrice;
    }

    public static CollateralDetail Create(
        string? engineNo,
        string? registrationNo,
        int? yearOfManufacture,
        string? countryOfManufacture,
        DateTime? purchaseDate,
        decimal? purchasePrice
    )
    {
        return new CollateralDetail(engineNo, registrationNo, yearOfManufacture, countryOfManufacture ,purchaseDate, purchasePrice);
    }
}