namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// Template definition for market comparable factors per property type.
/// Configuration entity that defines which factors are collected for each property type.
/// </summary>
public class MarketComparableTemplate : Entity<Guid>
{
    private readonly List<MarketComparableTemplateFactor> _templateFactors = [];
    public IReadOnlyList<MarketComparableTemplateFactor> TemplateFactors => _templateFactors.AsReadOnly();

    public string TemplateCode { get; private set; } = null!;
    public string TemplateName { get; private set; } = null!;
    public string PropertyType { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    private MarketComparableTemplate() { }

    public static MarketComparableTemplate Create(
        string templateCode,
        string templateName,
        string propertyType,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyType);

        return new MarketComparableTemplate
        {
            Id = Guid.CreateVersion7(),
            TemplateCode = templateCode.ToUpperInvariant(),
            TemplateName = templateName,
            PropertyType = propertyType,
            Description = description,
            IsActive = true
        };
    }

    public void Update(string templateName, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        TemplateName = templateName;
        Description = description;
    }

    public MarketComparableTemplateFactor AddFactor(
        Guid factorId,
        int displaySequence,
        bool isMandatory = false)
    {
        if (_templateFactors.Any(tf => tf.FactorId == factorId))
            throw new InvalidOperationException($"Factor {factorId} already exists in this template.");

        var templateFactor = MarketComparableTemplateFactor.Create(
            Id, factorId, displaySequence, isMandatory);
        _templateFactors.Add(templateFactor);
        return templateFactor;
    }

    public void RemoveFactor(Guid factorId)
    {
        var factor = _templateFactors.FirstOrDefault(tf => tf.FactorId == factorId);
        if (factor is null)
            return;

        _templateFactors.Remove(factor);
        Renumber();
    }

    public void ReorderFactors(IEnumerable<(Guid FactorId, int NewSequence)> reorderCommands)
    {
        TemplateFactorOrdering.Reorder(_templateFactors, reorderCommands);
    }

    // Reassigns a contiguous 1..n ordering (by current sequence) so no gaps or duplicates remain.
    private void Renumber()
    {
        var ordered = _templateFactors.OrderBy(tf => tf.DisplaySequence).ToList();
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].UpdateSequence(i + 1);
    }

    public void SetFactorMandatory(Guid factorId, bool isMandatory)
    {
        var factor = _templateFactors.FirstOrDefault(tf => tf.FactorId == factorId);
        factor?.SetMandatory(isMandatory);
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
