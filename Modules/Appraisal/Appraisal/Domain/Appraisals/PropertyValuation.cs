namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Junction table linking valuation to specific property detail records.
/// Uses polymorphic reference (PropertyDetailType + PropertyDetailId).
/// </summary>
public class PropertyValuation : Entity<Guid>
{
    public Guid ValuationAnalysisId { get; private set; }

    // Polymorphic Property Reference
    public string PropertyDetailType { get; private set; } =
        null!; // Land, Building, LandAndBuilding, Condo, Vehicle, Vessel, Machinery

    public Guid PropertyDetailId { get; private set; }

    // Individual Values
    public decimal MarketValue { get; private set; }
    public decimal AppraisedValue { get; private set; }
    public decimal? ForcedSaleValue { get; private set; }

    // Per-Unit Values
    public decimal? ValuePerUnit { get; private set; }
    public string? UnitType { get; private set; } // Sqm, Rai, Unit

    // Weight (for combined properties)
    public decimal? ValuationWeight { get; private set; }

    // Notes
    public string? ValuationNotes { get; private set; }

    private PropertyValuation()
    {
    }

    public static PropertyValuation Create(
        Guid valuationAnalysisId,
        string propertyDetailType,
        Guid propertyDetailId,
        decimal marketValue,
        decimal appraisedValue)
    {
        ValidatePropertyDetailType(propertyDetailType);

        return new PropertyValuation
        {
            Id = Guid.NewGuid(),
            ValuationAnalysisId = valuationAnalysisId,
            PropertyDetailType = propertyDetailType,
            PropertyDetailId = propertyDetailId,
            MarketValue = marketValue,
            AppraisedValue = appraisedValue
        };
    }

    private static void ValidatePropertyDetailType(string type)
    {
        var validTypes = new[] { "Land", "Building", "LandAndBuilding", "Condo", "Vehicle", "Vessel", "Machinery" };
        if (!validTypes.Contains(type))
            throw new ArgumentException($"Invalid property detail type: {type}");
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