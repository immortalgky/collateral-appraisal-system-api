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
    public bool IsSelected { get; private set; }

    private PricingAnalysisApproach()
    {
        // For EF Core
    }

    public static PricingAnalysisApproach Create(
        Guid pricingAnalysisId,
        string approachType)
    {
        return new PricingAnalysisApproach
        {
            // Id = Guid.NewGuid(),
            PricingAnalysisId = pricingAnalysisId,
            ApproachType = approachType,
            IsSelected = false
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

    public void Select()
    {
        IsSelected = true;
    }
}