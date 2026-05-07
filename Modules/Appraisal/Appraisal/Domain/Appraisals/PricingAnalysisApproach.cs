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
            Id = Guid.CreateVersion7(),
            PricingAnalysisId = pricingAnalysisId,
            ApproachType = approachType,
            IsSelected = false
        };
    }

    /// <summary>Deep-clone for CI carry-forward — rebuilds Methods chain. <paramref name="propertyIdMap"/> is threaded into MachineCostItem cloning.</summary>
    public static PricingAnalysisApproach CloneForAnalysis(
        PricingAnalysisApproach source,
        Guid newAnalysisId,
        IReadOnlyDictionary<Guid, Guid>? propertyIdMap = null)
    {
        var clone = new PricingAnalysisApproach
        {
            Id = Guid.CreateVersion7(),
            PricingAnalysisId = newAnalysisId,
            ApproachType = source.ApproachType,
            ApproachValue = source.ApproachValue,
            IsSelected = source.IsSelected
        };

        foreach (var m in source.Methods)
            clone._methods.Add(PricingAnalysisMethod.CloneForApproach(m, clone.Id, propertyIdMap));

        return clone;
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

    public void ClearValue()
    {
        ApproachValue = null;
    }

    public void Select()
    {
        IsSelected = true;
    }

    public void Unselect()
    {
        IsSelected = false;
    }

    public void RemoveMethod(Guid methodId)
    {
        var method = _methods.FirstOrDefault(m => m.Id == methodId);
        if (method is null)
            throw new InvalidOperationException($"Method with ID {methodId} not found in approach.");

        _methods.Remove(method);
    }
}