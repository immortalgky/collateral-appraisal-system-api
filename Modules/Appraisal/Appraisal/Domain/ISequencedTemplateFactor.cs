namespace Appraisal.Domain;

/// <summary>
/// A template factor that carries a display ordering and can be resequenced. Implemented by both
/// <see cref="ComparativeAnalysis.ComparativeAnalysisTemplateFactor"/> and
/// <see cref="MarketComparables.MarketComparableTemplateFactor"/> so the shared reorder algorithm
/// (<see cref="TemplateFactorOrdering"/>) can be written once.
/// </summary>
public interface ISequencedTemplateFactor
{
    Guid FactorId { get; }
    int DisplaySequence { get; }
    void UpdateSequence(int sequence);
}
