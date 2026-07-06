namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// Junction entity linking templates to factors with display ordering.
/// Managed as a child of MarketComparableTemplate.
/// </summary>
public class MarketComparableTemplateFactor : Entity<Guid>, ISequencedTemplateFactor
{
    public Guid TemplateId { get; private set; }
    public Guid FactorId { get; private set; }
    public int DisplaySequence { get; private set; }
    public bool IsMandatory { get; private set; }

    // Navigation property for eager loading
    public MarketComparableFactor? Factor { get; private set; }

    private MarketComparableTemplateFactor()
    {
    }

    internal static MarketComparableTemplateFactor Create(
        Guid templateId,
        Guid factorId,
        int displaySequence,
        bool isMandatory)
    {
        return new MarketComparableTemplateFactor
        {
            //Id = Guid.NewGuid(),
            TemplateId = templateId,
            FactorId = factorId,
            DisplaySequence = displaySequence,
            IsMandatory = isMandatory
        };
    }

    internal void UpdateSequence(int newSequence)
    {
        DisplaySequence = newSequence;
    }

    // Explicit interface implementation delegates to the internal method so the shared reorder
    // helper can resequence factors without widening UpdateSequence's accessibility.
    void ISequencedTemplateFactor.UpdateSequence(int sequence) => UpdateSequence(sequence);

    internal void SetMandatory(bool isMandatory)
    {
        IsMandatory = isMandatory;
    }
}