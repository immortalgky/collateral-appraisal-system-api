using Appraisal.Domain.Projects.Exceptions;

namespace Appraisal.Domain.Projects;

/// <summary>
/// Pricing assumptions for a project.
/// Superset of CondoPricingAssumption + VillagePricingAssumption.
/// Condo-only fields: PoolViewAdjustment, SouthAdjustment, FloorIncrementEveryXFloor, FloorIncrementAmount.
/// LB-only fields: NearGardenAdjustment, LandIncreaseDecreaseRate.
/// </summary>
public class ProjectPricingAssumption : Entity<Guid>
{
    public Guid ProjectId { get; private set; }

    // Location Method
    public string? LocationMethod { get; private set; }

    // ----- Shared Adjustments -----
    public decimal? CornerAdjustment { get; private set; }
    public decimal? EdgeAdjustment { get; private set; }
    public decimal? OtherAdjustment { get; private set; }
    public decimal? ForceSalePercentage { get; private set; }

    // ----- Condo-Only (nullable) -----
    public decimal? PoolViewAdjustment { get; private set; }
    public decimal? SouthAdjustment { get; private set; }
    public int? FloorIncrementEveryXFloor { get; private set; }
    public decimal? FloorIncrementAmount { get; private set; }

    // ----- LB-Only (nullable) -----
    public decimal? NearGardenAdjustment { get; private set; }
    public decimal? LandIncreaseDecreaseRate { get; private set; }

    // Model Assumptions (owned collection)
    private readonly List<ProjectModelAssumption> _modelAssumptions = [];
    public IReadOnlyList<ProjectModelAssumption> ModelAssumptions => _modelAssumptions.AsReadOnly();

    private ProjectPricingAssumption()
    {
    }

    public static ProjectPricingAssumption Create(Guid projectId)
    {
        return new ProjectPricingAssumption
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId
        };
    }

    /// <summary>Updates condo pricing assumptions. Rejects LB-only fields.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void UpdateCondo(
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
        ValidateCommon(forceSalePercentage, floorIncrementEveryXFloor);

        LocationMethod = locationMethod;
        CornerAdjustment = cornerAdjustment;
        EdgeAdjustment = edgeAdjustment;
        OtherAdjustment = otherAdjustment;
        ForceSalePercentage = forceSalePercentage;

        // Condo-specific
        PoolViewAdjustment = poolViewAdjustment;
        SouthAdjustment = southAdjustment;
        FloorIncrementEveryXFloor = floorIncrementEveryXFloor;
        FloorIncrementAmount = floorIncrementAmount;

        // LB cleared
        NearGardenAdjustment = null;
        LandIncreaseDecreaseRate = null;
    }

    /// <summary>Updates LandAndBuilding pricing assumptions. Rejects Condo-only fields.</summary>
    public void UpdateLandAndBuilding(
        string? locationMethod,
        decimal? cornerAdjustment,
        decimal? edgeAdjustment,
        decimal? nearGardenAdjustment,
        decimal? otherAdjustment,
        decimal? landIncreaseDecreaseRate,
        decimal? forceSalePercentage)
    {
        ValidateCommon(forceSalePercentage, null);

        LocationMethod = locationMethod;
        CornerAdjustment = cornerAdjustment;
        EdgeAdjustment = edgeAdjustment;
        OtherAdjustment = otherAdjustment;
        ForceSalePercentage = forceSalePercentage;

        // LB-specific
        NearGardenAdjustment = nearGardenAdjustment;
        LandIncreaseDecreaseRate = landIncreaseDecreaseRate;

        // Condo cleared
        PoolViewAdjustment = null;
        SouthAdjustment = null;
        FloorIncrementEveryXFloor = null;
        FloorIncrementAmount = null;
    }

    /// <summary>
    /// Replaces model assumptions, validating that every ProjectModelId belongs to the project.
    /// Throws <see cref="InvalidProjectStateException"/> if any id is not in <paramref name="validModelIds"/>.
    /// </summary>
    public void ReplaceModelAssumptions(
        IEnumerable<ProjectModelAssumption> assumptions,
        IReadOnlySet<Guid> validModelIds)
    {
        var list = assumptions.ToList();
        var invalid = list
            .Where(a => !validModelIds.Contains(a.ProjectModelId))
            .Select(a => a.ProjectModelId)
            .ToList();

        if (invalid.Count > 0)
            throw new InvalidProjectStateException(
                $"The following ProjectModelIds do not belong to this project: {string.Join(", ", invalid)}.");

        _modelAssumptions.Clear();
        _modelAssumptions.AddRange(list);
    }

    /// <summary>Replaces model assumptions without validation (internal use; caller must pre-validate).</summary>
    public void SetModelAssumptions(List<ProjectModelAssumption> assumptions)
    {
        _modelAssumptions.Clear();
        _modelAssumptions.AddRange(assumptions);
    }

    private static void ValidateCommon(decimal? forceSalePercentage, int? floorIncrementEveryXFloor)
    {
        if (forceSalePercentage is < 0 or > 100)
            throw new ArgumentException("Force sale percentage must be between 0 and 100", nameof(forceSalePercentage));
        if (floorIncrementEveryXFloor is < 0)
            throw new ArgumentException("Floor increment interval cannot be negative", nameof(floorIncrementEveryXFloor));
    }
}
