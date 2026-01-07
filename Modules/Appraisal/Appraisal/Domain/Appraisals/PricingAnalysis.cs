namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Pricing analysis container per appraisal (1:1 relationship).
/// </summary>
public class PricingAnalysis : Entity<Guid>
{
    private readonly List<PricingAnalysisApproach> _approaches = [];
    public IReadOnlyList<PricingAnalysisApproach> Approaches => _approaches.AsReadOnly();

    public Guid AppraisalId { get; private set; }

    // Status
    public string Status { get; private set; } = null!; // Draft, InProgress, Completed

    // Final Values
    public decimal? FinalMarketValue { get; private set; }
    public decimal? FinalAppraisedValue { get; private set; }
    public decimal? FinalForcedSaleValue { get; private set; }
    public DateTime? ValuationDate { get; private set; }

    private PricingAnalysis()
    {
    }

    public static PricingAnalysis Create(Guid appraisalId)
    {
        return new PricingAnalysis
        {
            Id = Guid.NewGuid(),
            AppraisalId = appraisalId,
            Status = "Draft"
        };
    }

    public PricingAnalysisApproach AddApproach(string approachType, decimal? weight = null)
    {
        if (approachType != "Market" && approachType != "Cost" && approachType != "Income")
            throw new ArgumentException("ApproachType must be 'Market', 'Cost', or 'Income'");

        if (_approaches.Any(a => a.ApproachType == approachType && a.Status == "Active"))
            throw new InvalidOperationException($"Approach '{approachType}' already exists");

        var approach = PricingAnalysisApproach.Create(Id, approachType, weight);
        _approaches.Add(approach);
        return approach;
    }

    public void StartProgress()
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Cannot start analysis in status '{Status}'");

        Status = "InProgress";
    }

    public void Complete(decimal marketValue, decimal appraisedValue, decimal? forcedSaleValue = null)
    {
        if (Status != "InProgress")
            throw new InvalidOperationException($"Cannot complete analysis in status '{Status}'");

        FinalMarketValue = marketValue;
        FinalAppraisedValue = appraisedValue;
        FinalForcedSaleValue = forcedSaleValue;
        ValuationDate = DateTime.UtcNow;
        Status = "Completed";
    }

    public void SetFinalValues(decimal marketValue, decimal appraisedValue, decimal? forcedSaleValue = null)
    {
        FinalMarketValue = marketValue;
        FinalAppraisedValue = appraisedValue;
        FinalForcedSaleValue = forcedSaleValue;
        ValuationDate = DateTime.UtcNow;
    }
}