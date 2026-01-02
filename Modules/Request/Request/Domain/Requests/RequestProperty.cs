namespace Request.Domain.Requests;

public class RequestProperty : ValueObject
{
    public string? PropertyType { get; }
    public string? BuildingType { get; }
    public decimal? SellingPrice { get; }

    private RequestProperty(string? propertyType, string? buildingType, decimal? sellingPrice)
    {
        PropertyType = propertyType;
        BuildingType = buildingType;
        SellingPrice = sellingPrice;
    }

    public static RequestProperty Create(
        string? propertyType,
        string? buildingType,
        decimal? sellingPrice
    )
    {
        return new RequestProperty(propertyType, buildingType, sellingPrice);
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PropertyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(BuildingType);
        if (SellingPrice is null || SellingPrice <= 0)
            throw new ArgumentException("SellingPrice must be greater than zero.");
    }
}