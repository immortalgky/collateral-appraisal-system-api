namespace Request.Requests.ValueObjects;

public class RequestProperty : ValueObject
{
    public string? PropertyType { get; }
    public string? BuildingType { get; }
    public decimal? SellingPrice { get; }

#pragma warning disable CS8618
    private RequestProperty()
    {
        // For EF Core
    }
#pragma warning restore CS8618

    private RequestProperty(
        string propertyType,
        string buildingType,
        decimal? sellingPrice
    )
    {
        PropertyType = propertyType;
        BuildingType = buildingType;
        SellingPrice = sellingPrice;
    }

    public static RequestProperty Of(
        string? propertyType,
        string? buildingType,
        decimal? sellingPrice
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyType);
        ArgumentException.ThrowIfNullOrEmpty(buildingType);

        return new RequestProperty(
            propertyType,
            buildingType,
            sellingPrice
        );
    }
}