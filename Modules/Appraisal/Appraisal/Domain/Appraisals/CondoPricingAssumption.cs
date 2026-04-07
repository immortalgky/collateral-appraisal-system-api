namespace Appraisal.Domain.Appraisals;

public class CondoPricingAssumption : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }

    // Location Method
    public string? LocationMethod { get; private set; }

    // Adjustments
    public decimal? CornerAdjustment { get; private set; }
    public decimal? EdgeAdjustment { get; private set; }
    public decimal? PoolViewAdjustment { get; private set; }
    public decimal? SouthAdjustment { get; private set; }
    public decimal? OtherAdjustment { get; private set; }

    // Floor Increment
    public int? FloorIncrementEveryXFloor { get; private set; }
    public decimal? FloorIncrementAmount { get; private set; }

    // Force Sale
    public decimal? ForceSalePercentage { get; private set; }

    // Model Assumptions (owned collection)
    private readonly List<CondoModelAssumption> _modelAssumptions = [];
    public IReadOnlyList<CondoModelAssumption> ModelAssumptions => _modelAssumptions.AsReadOnly();

    private CondoPricingAssumption()
    {
    }

    public static CondoPricingAssumption Create(Guid appraisalId)
    {
        return new CondoPricingAssumption
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
        decimal? poolViewAdjustment,
        decimal? southAdjustment,
        decimal? otherAdjustment,
        int? floorIncrementEveryXFloor,
        decimal? floorIncrementAmount,
        decimal? forceSalePercentage)
    {
        // Validation
        if (forceSalePercentage is < 0 or > 100)
            throw new ArgumentException("Force sale percentage must be between 0 and 100", nameof(forceSalePercentage));
        if (floorIncrementEveryXFloor is < 0)
            throw new ArgumentException("Floor increment interval cannot be negative", nameof(floorIncrementEveryXFloor));

        LocationMethod = locationMethod;
        CornerAdjustment = cornerAdjustment;
        EdgeAdjustment = edgeAdjustment;
        PoolViewAdjustment = poolViewAdjustment;
        SouthAdjustment = southAdjustment;
        OtherAdjustment = otherAdjustment;
        FloorIncrementEveryXFloor = floorIncrementEveryXFloor;
        FloorIncrementAmount = floorIncrementAmount;
        ForceSalePercentage = forceSalePercentage;
    }

    public void SetModelAssumptions(List<CondoModelAssumption> assumptions)
    {
        _modelAssumptions.Clear();
        _modelAssumptions.AddRange(assumptions);
    }
}
