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

    private ComparativeAnalysisTemplate() { }

    public static ComparativeAnalysisTemplate Create(
        string templateCode,
        string templateName,
        string propertyType,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyType);

        var validPropertyTypes = new[] { "Land", "Building", "Condo", "Vehicle", "Vessel", "Machinery" };
        if (!validPropertyTypes.Contains(propertyType))
            throw new ArgumentException($"PropertyType must be one of: {string.Join(", ", validPropertyTypes)}");

        return new ComparativeAnalysisTemplate
        {
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

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public ComparativeAnalysisTemplateFactor AddFactor(
        Guid factorId,
        int displaySequence,
        bool isMandatory = false,
        decimal? defaultWeight = null)
    {
        if (_factors.Any(f => f.FactorId == factorId))
            throw new InvalidOperationException($"Factor {factorId} already exists in this template");

        var factor = ComparativeAnalysisTemplateFactor.Create(Id, factorId, displaySequence, isMandatory, defaultWeight);
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
        foreach (var f in _factors.OrderBy(x => x.DisplaySequence))
        {
            f.UpdateSequence(sequence++);
        }
    }

    public void UpdateFactorSequence(Guid factorId, int newSequence)
    {
        var factor = _factors.FirstOrDefault(f => f.FactorId == factorId)
            ?? throw new InvalidOperationException($"Factor {factorId} not found in this template");

        factor.UpdateSequence(newSequence);
    }
}
