namespace Appraisal.Domain.Projects;

public enum StandardPriceUnit
{
    PerSquareMeter = 1,
    BahtPerUnit = 2,
}

/// <summary>
/// Per-model assumptions owned by ProjectPricingAssumption.
/// Superset of CondoModelAssumption + VillageModelAssumption.
/// StandardLandPrice is LB-only (nullable).
/// </summary>
public class ProjectModelAssumption
{
    public Guid Id { get; private set; }
    public Guid ProjectModelId { get; private set; }
    public string? ModelType { get; private set; }
    public string? ModelDescription { get; private set; }
    public decimal? UsableAreaFrom { get; private set; }
    public decimal? UsableAreaTo { get; private set; }
    public decimal? StandardLandPrice { get; private set; } // LB only
    public StandardPriceUnit StandardPriceUnit { get; private set; } = StandardPriceUnit.BahtPerUnit;
    public decimal? CoverageAmount { get; private set; }
    public string? FireInsuranceCondition { get; private set; }

    private ProjectModelAssumption()
    {
    }

    public static ProjectModelAssumption Create(
        Guid projectModelId,
        string? modelType,
        string? modelDescription,
        decimal? usableAreaFrom,
        decimal? usableAreaTo,
        decimal? standardLandPrice,
        StandardPriceUnit standardPriceUnit,
        decimal? coverageAmount,
        string? fireInsuranceCondition)
    {
        return new ProjectModelAssumption
        {
            Id = Guid.CreateVersion7(),
            ProjectModelId = projectModelId,
            ModelType = modelType,
            ModelDescription = modelDescription,
            UsableAreaFrom = usableAreaFrom,
            UsableAreaTo = usableAreaTo,
            StandardLandPrice = standardLandPrice,
            StandardPriceUnit = standardPriceUnit,
            CoverageAmount = coverageAmount,
            FireInsuranceCondition = fireInsuranceCondition
        };
    }

    public void Update(
        Guid projectModelId,
        string? modelType,
        string? modelDescription,
        decimal? usableAreaFrom,
        decimal? usableAreaTo,
        decimal? standardLandPrice,
        StandardPriceUnit standardPriceUnit,
        decimal? coverageAmount,
        string? fireInsuranceCondition)
    {
        ProjectModelId = projectModelId;
        ModelType = modelType;
        ModelDescription = modelDescription;
        UsableAreaFrom = usableAreaFrom;
        UsableAreaTo = usableAreaTo;
        StandardLandPrice = standardLandPrice;
        StandardPriceUnit = standardPriceUnit;
        CoverageAmount = coverageAmount;
        FireInsuranceCondition = fireInsuranceCondition;
    }
}
