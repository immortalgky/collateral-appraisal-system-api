namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Pricing analysis container per PropertyGroup (1:1 relationship).
/// </summary>
public class PricingAnalysis : Aggregate<Guid>
{
    private readonly List<PricingAnalysisApproach> _approaches = [];
    public IReadOnlyList<PricingAnalysisApproach> Approaches => _approaches.AsReadOnly();

    public Guid PropertyGroupId { get; private set; }

    // Status
    public string Status { get; private set; } = null!; // Draft, InProgress, Completed

    // Final Values
    public decimal? FinalAppraisedValue { get; private set; }

    private PricingAnalysis()
    {
    }

    public static PricingAnalysis Create(Guid propertyGroupId)
    {
        return new PricingAnalysis
        {
            Id = Guid.CreateVersion7(),
            PropertyGroupId = propertyGroupId,
            Status = "Draft"
        };
    }

    public PricingAnalysisApproach AddApproach(string approachType, decimal? weight = null)
    {
        if (approachType != "Market" && approachType != "Cost" && approachType != "Income")
            throw new ArgumentException("ApproachType must be 'Market', 'Cost', or 'Income'");

        if (_approaches.Any(a => a.ApproachType == approachType))
            throw new InvalidOperationException($"Approach '{approachType}' already exists");

        var approach = PricingAnalysisApproach.Create(Id, approachType);
        _approaches.Add(approach);
        return approach;
    }

    public void StartProgress()
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Cannot start analysis in status '{Status}'");

        Status = "InProgress";
    }

    public void Complete(decimal appraisedValue)
    {
        if (Status != "InProgress")
            throw new InvalidOperationException($"Cannot complete analysis in status '{Status}'");

        FinalAppraisedValue = appraisedValue;
        Status = "Completed";
    }

    public void SetFinalValues(decimal appraisedValue)
    {
        FinalAppraisedValue = appraisedValue;
    }
}