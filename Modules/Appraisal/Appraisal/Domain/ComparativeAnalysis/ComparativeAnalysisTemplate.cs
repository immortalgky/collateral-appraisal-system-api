namespace Appraisal.Domain.ComparativeAnalysis;

/// <summary>
/// Master data template that defines which factors are available for comparative analysis
/// based on property type. Each property type can have its own template.
/// </summary>
public class ComparativeAnalysisTemplate : Entity<Guid>
{
    private readonly List<ComparativeAnalysisTemplateFactor> _factors = [];
    public IReadOnlyList<ComparativeAnalysisTemplateFactor> Factors => _factors.AsReadOnly();

    public string TemplateCode { get; private set; } = null!;
    public string TemplateName { get; private set; } = null!;
    public string PropertyType { get; private set; } = null!; // Land, Building, Condo, Vehicle, Vessel, Machinery
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ComparativeAnalysisTemplate()
    {
        // For EF Core
    }

    public static ComparativeAnalysisTemplate Create(
        string templateCode,
        string templateName,
        string propertyType,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyType);

        return new ComparativeAnalysisTemplate
        {
            Id = Guid.CreateVersion7(),
            TemplateCode = templateCode.ToUpperInvariant(),
            TemplateName = templateName,
            PropertyType = Appraisals.PropertyType.FromString(propertyType),
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

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public ComparativeAnalysisTemplateFactor AddFactor(
        Guid factorId,
        int displaySequence,
        bool isMandatory = false,
        decimal? defaultWeight = null,
        decimal? defaultIntensity = null,
        bool isCalculationFactor = false)
    {
        if (_factors.Any(f => f.FactorId == factorId))
            throw new InvalidOperationException($"Factor {factorId} already exists in this template");

        var factor =
            ComparativeAnalysisTemplateFactor.Create(Id, factorId, displaySequence, isMandatory, defaultWeight, defaultIntensity, isCalculationFactor);
        _factors.Add(factor);
        return factor;
    }

    public void RemoveFactor(Guid factorId)
    {
        var factor = _factors.FirstOrDefault(f => f.FactorId == factorId);
        if (factor is null)
            throw new InvalidOperationException($"Factor {factorId} not found in this template");

        _factors.Remove(factor);

        // Resequence remaining factors
        var sequence = 1;
        foreach (var f in _factors.OrderBy(x => x.DisplaySequence)) f.UpdateSequence(sequence++);
    }

    public void UpdateFactorSequence(Guid factorId, int newSequence)
    {
        var factor = _factors.FirstOrDefault(f => f.FactorId == factorId)
                     ?? throw new NotFoundException("ComparativeAnalysisTemplateFactor", factorId);

        factor.UpdateSequence(newSequence);
    }

    public void ReorderFactors(IEnumerable<(Guid FactorId, int NewSequence)> reorderCommands)
    {
        TemplateFactorOrdering.Reorder(_factors, reorderCommands);
    }

    public void UpdateFactor(
        Guid factorId,
        bool isMandatory,
        decimal? defaultWeight,
        decimal? defaultIntensity,
        bool isCalculationFactor)
    {
        var factor = _factors.FirstOrDefault(f => f.FactorId == factorId)
                     ?? throw new NotFoundException("ComparativeAnalysisTemplateFactor", factorId);

        // In-place field update — never touches DisplaySequence, so it cannot collide with
        // the resequencing done by RemoveFactor (unlike the old remove+add toggle flow).
        factor.Update(isMandatory, defaultWeight, defaultIntensity, isCalculationFactor);
    }
}