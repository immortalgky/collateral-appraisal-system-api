namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Approach selection per pricing analysis (Market, Cost, Income).
/// </summary>
public class PricingAnalysisApproach : Entity<Guid>
{
    private readonly List<PricingAnalysisMethod> _methods = [];
    public IReadOnlyList<PricingAnalysisMethod> Methods => _methods.AsReadOnly();

    public Guid PricingAnalysisId { get; private set; }

    // Approach
    public string ApproachType { get; private set; } = null!; // Market, Cost, Income
    public decimal? ApproachValue { get; private set; }
    public decimal? Weight { get; private set; }
    public string Status { get; private set; } = null!; // Active, Excluded
    public string? ExclusionReason { get; private set; }

    private PricingAnalysisApproach()
    {
    }

    public static PricingAnalysisApproach Create(
        Guid pricingAnalysisId,
        string approachType,
        decimal? weight = null)
    {
        return new PricingAnalysisApproach
        {
            Id = Guid.NewGuid(),
            PricingAnalysisId = pricingAnalysisId,
            ApproachType = approachType,
            Weight = weight,
            Status = "Active"
        };
    }

    public PricingAnalysisMethod AddMethod(string methodType, string status = "Selected")
    {
        var method = PricingAnalysisMethod.Create(Id, methodType, status);
        _methods.Add(method);
        return method;
    }

    public void SetValue(decimal value)
    {
        ApproachValue = value;
    }

    public void SetWeight(decimal weight)
    {
        if (weight < 0 || weight > 100)
            throw new ArgumentException("Weight must be between 0 and 100");

        Weight = weight;
    }

    public void Exclude(string reason)
    {
        Status = "Excluded";
        ExclusionReason = reason;
    }

    public void Activate()
    {
        Status = "Active";
        ExclusionReason = null;
    }
}