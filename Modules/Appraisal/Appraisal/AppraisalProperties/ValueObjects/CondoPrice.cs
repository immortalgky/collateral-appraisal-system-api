namespace Appraisal.AppraisalProperties.ValueObjects;

public class CondoPrice : ValueObject
{
    public decimal? BuildingInsurancePrice { get; }
    public decimal? SellingPrice { get; }
    public decimal? ForceSellingPrice { get; }

    private CondoPrice(
        decimal? buildingInsurancePrice,
        decimal? sellingPrice,
        decimal? forceSellingPrice
    )
    {
        BuildingInsurancePrice = buildingInsurancePrice;
        SellingPrice = sellingPrice;
        ForceSellingPrice = forceSellingPrice;
    }

    public static CondoPrice Create(
        decimal? buildingInsurancePrice,
        decimal? sellingPrice,
        decimal? forceSellingPrice
    )
    {
        return new CondoPrice(buildingInsurancePrice, sellingPrice, forceSellingPrice);
    }
}
