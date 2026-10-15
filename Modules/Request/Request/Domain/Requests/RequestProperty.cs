namespace Request.Domain.Requests;

public class RequestProperty : ValueObject
{
    public string? PropertyType { get; }
    public string? BuildingType { get; }
    public string? BuildingTypeOther { get; }
    public decimal? SellingPrice { get; }

    private RequestProperty(string? propertyType, string? buildingType, string? buildingTypeOther, decimal? sellingPrice)
    {
        PropertyType = propertyType;
        BuildingType = buildingType;
        BuildingTypeOther = buildingTypeOther;
        SellingPrice = sellingPrice;
    }

    public static RequestProperty Create(
        string? propertyType,
        string? buildingType,
        string? buildingTypeOther,
        decimal? sellingPrice
    )
    {
        return new RequestProperty(propertyType, buildingType, buildingTypeOther, sellingPrice);
    }

    public void Validate(string? bankingSegment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PropertyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(BuildingType);
        if (bankingSegment != "IBG")
        {
            if (SellingPrice is null || SellingPrice <= 0)
                throw new ArgumentException("SellingPrice must be greater than zero.");
        }
    }
}