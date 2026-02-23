namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// EAV (Entity-Attribute-Value) storage for factor values per comparable.
/// Child entity of MarketComparable aggregate.
/// </summary>
public class MarketComparableData : Entity<Guid>
{
    public Guid MarketComparableId { get; private set; }
    public Guid FactorId { get; private set; }
    public string? Value { get; private set; }
    public string? OtherRemarks { get; private set; }

    // Navigation for eager loading
    public MarketComparableFactor? Factor { get; private set; }

    private MarketComparableData()
    {
    }

    internal static MarketComparableData Create(
        Guid marketComparableId,
        Guid factorId,
        string? value,
        string? otherRemarks = null)
    {
        return new MarketComparableData
        {
            //Id = Guid.NewGuid(),
            MarketComparableId = marketComparableId,
            FactorId = factorId,
            Value = value,
            OtherRemarks = otherRemarks
        };
    }

    internal void UpdateValue(string? value, string? otherRemarks = null)
    {
        Value = value;
        OtherRemarks = otherRemarks;
    }
}