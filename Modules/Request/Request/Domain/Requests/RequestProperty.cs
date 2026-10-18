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

    // Property types with no building: BuildingType is not collected and is stored as null.
    // FE twin: NO_BUILDING_PROPERTY_TYPES in collateral-appraisal-system-app src/features/request/data/mapCollateral.ts; codes come from the PropertyType parameter seed.
    private static readonly HashSet<string> NoBuildingPropertyTypes = ["L", "LSL", "MAC", "VEH", "VES"];

    internal static bool HasNoBuilding(string? propertyType) =>
        propertyType is not null && NoBuildingPropertyTypes.Contains(propertyType);

    public static RequestProperty Create(
        string? propertyType,
        string? buildingType,
        string? buildingTypeOther,
        decimal? sellingPrice
    )
    {
        // Every path (create, update, LOS, reappraisal) builds through here, so a stale
        // BuildingType sent for a no-building type is dropped in one place.
        return HasNoBuilding(propertyType)
            ? new RequestProperty(propertyType, null, null, sellingPrice)
            : new RequestProperty(propertyType, buildingType, buildingTypeOther, sellingPrice);
    }

    public void Validate(string? bankingSegment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PropertyType);
        if (!HasNoBuilding(PropertyType))
            ArgumentException.ThrowIfNullOrWhiteSpace(BuildingType);
        if (bankingSegment != "IBG")
        {
            if (SellingPrice is null || SellingPrice <= 0)
                throw new ArgumentException("SellingPrice must be greater than zero.");
        }
    }
}