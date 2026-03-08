namespace Appraisal.Domain.MarketComparables;

public class MarketComparableFactorTranslation
{
    public Guid MarketComparableFactorId { get; private set; }
    public string Language { get; private set; } = null!;
    public string FactorName { get; private set; } = null!;

    private MarketComparableFactorTranslation() { }

    internal static MarketComparableFactorTranslation Create(string language, string factorName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(factorName);

        return new MarketComparableFactorTranslation
        {
            Language = language.ToLowerInvariant(),
            FactorName = factorName
        };
    }

    internal void UpdateFactorName(string factorName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factorName);
        FactorName = factorName;
    }
}
