namespace Appraisal.Domain.Appraisals;

public class VillageModelAssumption
{
    public Guid VillageModelId { get; private set; }
    public string? ModelType { get; private set; }
    public string? ModelDescription { get; private set; }
    public decimal? UsableAreaFrom { get; private set; }
    public decimal? UsableAreaTo { get; private set; }
    public decimal? StandardLandPrice { get; private set; }
    public decimal? StandardPrice { get; private set; }
    public decimal? CoverageAmount { get; private set; }
    public string? FireInsuranceCondition { get; private set; }

    private VillageModelAssumption()
    {
    }

    public static VillageModelAssumption Create(
        Guid villageModelId,
        string? modelType,
        string? modelDescription,
        decimal? usableAreaFrom,
        decimal? usableAreaTo,
        decimal? standardLandPrice,
        decimal? standardPrice,
        decimal? coverageAmount,
        string? fireInsuranceCondition)
    {
        return new VillageModelAssumption
        {
            VillageModelId = villageModelId,
            ModelType = modelType,
            ModelDescription = modelDescription,
            UsableAreaFrom = usableAreaFrom,
            UsableAreaTo = usableAreaTo,
            StandardLandPrice = standardLandPrice,
            StandardPrice = standardPrice,
            CoverageAmount = coverageAmount,
            FireInsuranceCondition = fireInsuranceCondition
        };
    }

    public void Update(
        Guid villageModelId,
        string? modelType,
        string? modelDescription,
        decimal? usableAreaFrom,
        decimal? usableAreaTo,
        decimal? standardLandPrice,
        decimal? standardPrice,
        decimal? coverageAmount,
        string? fireInsuranceCondition)
    {
        VillageModelId = villageModelId;
        ModelType = modelType;
        ModelDescription = modelDescription;
        UsableAreaFrom = usableAreaFrom;
        UsableAreaTo = usableAreaTo;
        StandardLandPrice = standardLandPrice;
        StandardPrice = standardPrice;
        CoverageAmount = coverageAmount;
        FireInsuranceCondition = fireInsuranceCondition;
    }
}
