namespace Appraisal.Domain.ComparativeAnalysis;

/// <summary>
/// Junction entity linking a template to its factors.
/// References the existing MarketComparableFactor entity.
/// </summary>
public class ComparativeAnalysisTemplateFactor : Entity<Guid>
{
    public Guid TemplateId { get; private set; }
    public Guid FactorId { get; private set; } // FK → MarketComparableFactor
    public int DisplaySequence { get; private set; }
    public bool IsMandatory { get; private set; }
    public decimal? DefaultWeight { get; private set; }

    private ComparativeAnalysisTemplateFactor() { }

    public static ComparativeAnalysisTemplateFactor Create(
        Guid templateId,
        Guid factorId,
        int displaySequence,
        bool isMandatory = false,
        decimal? defaultWeight = null)
    {
        if (defaultWeight.HasValue && (defaultWeight < 0 || defaultWeight > 100))
            throw new ArgumentException("DefaultWeight must be between 0 and 100");

        return new ComparativeAnalysisTemplateFactor
        {
            TemplateId = templateId,
            FactorId = factorId,
            DisplaySequence = displaySequence,
            IsMandatory = isMandatory,
            DefaultWeight = defaultWeight
        };
    }

    public void UpdateSequence(int sequence)
    {
        DisplaySequence = sequence;
    }

    public void Update(bool isMandatory, decimal? defaultWeight)
    {
        if (defaultWeight.HasValue && (defaultWeight < 0 || defaultWeight > 100))
            throw new ArgumentException("DefaultWeight must be between 0 and 100");

        IsMandatory = isMandatory;
        DefaultWeight = defaultWeight;
    }
}
