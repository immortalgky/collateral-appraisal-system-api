namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Valuation analysis entity - 1:1 with Appraisal.
/// Contains the overall valuation results and approach used.
/// </summary>
public class ValuationAnalysis : Entity<Guid>
{
    // Core Properties
    public Guid AppraisalId { get; private set; }
    public string ValuationApproach { get; private set; } = null!; // Market, Cost, Income, Combined
    public DateTime ValuationDate { get; private set; }

    // Total Values
    public decimal AppraisedValue { get; private set; }
    public decimal? ForcedSaleValue { get; private set; }
    public decimal? InsuranceValue { get; private set; }

    // Currency
    public string Currency { get; private set; } = "THB";

    // Appraiser Opinion
    public string? AppraiserOpinion { get; private set; }
    public string? ValuationNotes { get; private set; }

    private ValuationAnalysis()
    {
    }

    public static ValuationAnalysis Create(
        Guid appraisalId,
        string valuationApproach,
        DateTime valuationDate)
    {
        return new ValuationAnalysis
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            ValuationApproach = valuationApproach,
            ValuationDate = valuationDate
        };
    }

    public void UpdateSummary(
        string valuationApproach,
        DateTime valuationDate,
        decimal appraisedValue,
        decimal? forcedSaleValue,
        decimal? insuranceValue)
    {
        ValuationApproach = valuationApproach;
        ValuationDate = valuationDate;
        AppraisedValue = appraisedValue;
        ForcedSaleValue = forcedSaleValue;
        InsuranceValue = insuranceValue;
    }
}