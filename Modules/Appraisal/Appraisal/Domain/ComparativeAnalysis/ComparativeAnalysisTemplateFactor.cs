namespace Appraisal.Domain.ComparativeAnalysis;

/// <summary>
/// Junction entity linking a template to its factors.
/// References the existing MarketComparableFactor entity.
/// </summary>
public class ComparativeAnalysisTemplateFactor : Entity<Guid>, ISequencedTemplateFactor
{
    public Guid TemplateId { get; private set; }
    public Guid FactorId { get; private set; } // FK → MarketComparableFactor
    public int DisplaySequence { get; private set; }
    public bool IsMandatory { get; private set; }
    public decimal? DefaultWeight { get; private set; }
    public decimal? DefaultIntensity { get; private set; }
    public bool IsCalculationFactor { get; private set; }

    private ComparativeAnalysisTemplateFactor() { }

    public static ComparativeAnalysisTemplateFactor Create(
        Guid templateId,
        Guid factorId,
        int displaySequence,
        bool isMandatory = false,
        decimal? defaultWeight = null,
        decimal? defaultIntensity = null,
        bool isCalculationFactor = false)
    {
        if (isCalculationFactor && defaultWeight.HasValue && (defaultWeight < 0 || defaultWeight > 100))
            throw new ArgumentException("DefaultWeight must be between 0 and 100");

        return new ComparativeAnalysisTemplateFactor
        {
            TemplateId = templateId,
            FactorId = factorId,
            DisplaySequence = displaySequence,
            IsMandatory = isMandatory,
            // Weight/intensity only apply to calculation factors; keep them null otherwise.
            DefaultWeight = isCalculationFactor ? defaultWeight : null,
            DefaultIntensity = isCalculationFactor ? defaultIntensity : null,
            IsCalculationFactor = isCalculationFactor
        };
    }

    public void UpdateSequence(int sequence)
    {
        DisplaySequence = sequence;
    }

    public void Update(bool isMandatory, decimal? defaultWeight, decimal? defaultIntensity = null, bool isCalculationFactor = false)
    {
        if (isCalculationFactor && defaultWeight.HasValue && (defaultWeight < 0 || defaultWeight > 100))
            throw new ArgumentException("DefaultWeight must be between 0 and 100");

        IsMandatory = isMandatory;
        IsCalculationFactor = isCalculationFactor;

        // Weight and intensity only apply to calculation factors. Clear the stored values when a
        // factor is not (or no longer) a calculation factor so no stale data is left behind.
        DefaultWeight = isCalculationFactor ? defaultWeight : null;
        DefaultIntensity = isCalculationFactor ? defaultIntensity : null;
    }
}
