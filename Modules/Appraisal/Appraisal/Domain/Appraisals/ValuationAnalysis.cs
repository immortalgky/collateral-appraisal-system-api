namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Valuation analysis entity - 1:1 with Appraisal.
/// Contains the overall valuation results and approach used.
/// </summary>
public class ValuationAnalysis : Entity<Guid>
{
    private readonly List<GroupValuation> _groupValuations = [];
    private readonly List<PropertyValuation> _propertyValuations = [];

    public IReadOnlyList<GroupValuation> GroupValuations => _groupValuations.AsReadOnly();
    public IReadOnlyList<PropertyValuation> PropertyValuations => _propertyValuations.AsReadOnly();

    // Core Properties
    public Guid AppraisalId { get; private set; }
    public string ValuationApproach { get; private set; } = null!; // Market, Cost, Income, Combined
    public DateTime ValuationDate { get; private set; }

    // Total Values
    public decimal MarketValue { get; private set; }
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
            Id = Guid.NewGuid(),
            AppraisalId = appraisalId,
            ValuationApproach = valuationApproach,
            ValuationDate = valuationDate
        };
    }

    public void SetValues(
        decimal marketValue,
        decimal appraisedValue,
        decimal? forcedSaleValue = null,
        decimal? insuranceValue = null)
    {
        MarketValue = marketValue;
        AppraisedValue = appraisedValue;
        ForcedSaleValue = forcedSaleValue;
        InsuranceValue = insuranceValue;
    }

    public void SetOpinion(string? opinion, string? notes)
    {
        AppraiserOpinion = opinion;
        ValuationNotes = notes;
    }

    public GroupValuation AddGroupValuation(
        Guid collateralGroupId,
        decimal marketValue,
        decimal appraisedValue)
    {
        var groupValuation = GroupValuation.Create(
            Id, collateralGroupId, marketValue, appraisedValue);
        _groupValuations.Add(groupValuation);
        return groupValuation;
    }

    public PropertyValuation AddPropertyValuation(
        string propertyDetailType,
        Guid propertyDetailId,
        decimal marketValue,
        decimal appraisedValue)
    {
        var propertyValuation = PropertyValuation.Create(
            Id, propertyDetailType, propertyDetailId, marketValue, appraisedValue);
        _propertyValuations.Add(propertyValuation);
        return propertyValuation;
    }
}