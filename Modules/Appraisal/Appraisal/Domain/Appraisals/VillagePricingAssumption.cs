namespace Appraisal.Domain.Appraisals;

public class VillagePricingAssumption : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }

    // Location Method
    public string? LocationMethod { get; private set; }

    // Adjustments
    public decimal? CornerAdjustment { get; private set; }
    public decimal? EdgeAdjustment { get; private set; }
    public decimal? NearGardenAdjustment { get; private set; }
    public decimal? OtherAdjustment { get; private set; }

    // Land Increase/Decrease
    public decimal? LandIncreaseDecreaseRate { get; private set; }

    // Force Sale
    public decimal? ForceSalePercentage { get; private set; }

    // Model Assumptions (owned collection)
    private readonly List<VillageModelAssumption> _modelAssumptions = [];
    public IReadOnlyList<VillageModelAssumption> ModelAssumptions => _modelAssumptions.AsReadOnly();

    private VillagePricingAssumption()
    {
    }

    public static VillagePricingAssumption Create(Guid appraisalId)
    {
        return new VillagePricingAssumption
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void Update(
        string? locationMethod,
        decimal? cornerAdjustment,
        decimal? edgeAdjustment,
        decimal? nearGardenAdjustment,
        decimal? otherAdjustment,
        decimal? landIncreaseDecreaseRate,
        decimal? forceSalePercentage)
    {
        // Validation
        if (forceSalePercentage is < 0 or > 100)
            throw new ArgumentException("Force sale percentage must be between 0 and 100", nameof(forceSalePercentage));

        LocationMethod = locationMethod;
        CornerAdjustment = cornerAdjustment;
        EdgeAdjustment = edgeAdjustment;
        NearGardenAdjustment = nearGardenAdjustment;
        OtherAdjustment = otherAdjustment;
        LandIncreaseDecreaseRate = landIncreaseDecreaseRate;
        ForceSalePercentage = forceSalePercentage;
    }

    public void SetModelAssumptions(List<VillageModelAssumption> assumptions)
    {
        _modelAssumptions.Clear();
        _modelAssumptions.AddRange(assumptions);
    }
}
