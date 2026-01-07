namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Valuation for a specific property group.
/// </summary>
public class GroupValuation : Entity<Guid>
{
    public Guid ValuationAnalysisId { get; private set; }
    public Guid PropertyGroupId { get; private set; }

    // Values
    public decimal MarketValue { get; private set; }
    public decimal AppraisedValue { get; private set; }
    public decimal? ForcedSaleValue { get; private set; }

    // Per-Unit Values
    public decimal? ValuePerUnit { get; private set; }
    public string? UnitType { get; private set; } // Sqm, Rai, Unit

    // Weight for combined properties
    public decimal? ValuationWeight { get; private set; }

    // Notes
    public string? ValuationNotes { get; private set; }

    private GroupValuation()
    {
    }

    public static GroupValuation Create(
        Guid valuationAnalysisId,
        Guid propertyGroupId,
        decimal marketValue,
        decimal appraisedValue)
    {
        return new GroupValuation
        {
            Id = Guid.NewGuid(),
            ValuationAnalysisId = valuationAnalysisId,
            PropertyGroupId = propertyGroupId,
            MarketValue = marketValue,
            AppraisedValue = appraisedValue
        };
    }

    public void SetForcedSaleValue(decimal value)
    {
        ForcedSaleValue = value;
    }

    public void SetPerUnitValue(decimal valuePerUnit, string unitType)
    {
        ValuePerUnit = valuePerUnit;
        UnitType = unitType;
    }

    public void SetWeight(decimal weight)
    {
        if (weight < 0 || weight > 100)
            throw new ArgumentException("Weight must be between 0 and 100");
        ValuationWeight = weight;
    }
}