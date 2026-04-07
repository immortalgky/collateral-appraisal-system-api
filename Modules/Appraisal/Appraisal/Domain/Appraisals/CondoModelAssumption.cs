namespace Appraisal.Domain.Appraisals;

public class CondoModelAssumption
{
    public Guid CondoModelId { get; private set; }
    public string? ModelType { get; private set; }
    public string? ModelDescription { get; private set; }
    public decimal? UsableAreaFrom { get; private set; }
    public decimal? UsableAreaTo { get; private set; }
    public decimal? StandardPrice { get; private set; }
    public decimal? CoverageAmount { get; private set; }
    public string? FireInsuranceCondition { get; private set; }

    private CondoModelAssumption()
    {
    }

    public static CondoModelAssumption Create(
        Guid condoModelId,
        string? modelType,
        string? modelDescription,
        decimal? usableAreaFrom,
        decimal? usableAreaTo,
        decimal? standardPrice,
        decimal? coverageAmount,
        string? fireInsuranceCondition)
    {
        return new CondoModelAssumption
        {
            CondoModelId = condoModelId,
            ModelType = modelType,
            ModelDescription = modelDescription,
            UsableAreaFrom = usableAreaFrom,
            UsableAreaTo = usableAreaTo,
            StandardPrice = standardPrice,
            CoverageAmount = coverageAmount,
            FireInsuranceCondition = fireInsuranceCondition
        };
    }

    public void Update(
        Guid condoModelId,
        string? modelType,
        string? modelDescription,
        decimal? usableAreaFrom,
        decimal? usableAreaTo,
        decimal? standardPrice,
        decimal? coverageAmount,
        string? fireInsuranceCondition)
    {
        CondoModelId = condoModelId;
        ModelType = modelType;
        ModelDescription = modelDescription;
        UsableAreaFrom = usableAreaFrom;
        UsableAreaTo = usableAreaTo;
        StandardPrice = standardPrice;
        CoverageAmount = coverageAmount;
        FireInsuranceCondition = fireInsuranceCondition;
    }
}
