namespace Parameter.PricingTemplates.Models;

/// <summary>
/// Master template blueprint for the income-approach pricing analysis.
/// Seeded from frontend dcfTemplates.ts — admin-editable shape, no computed totals.
/// </summary>
public class PricingTemplate : Entity<Guid>
{
    private readonly List<PricingTemplateSection> _sections = [];

    public IReadOnlyCollection<PricingTemplateSection> Sections => _sections.AsReadOnly();

    /// <summary>Unique code, e.g. "dcf-hotel", "direct-apartment".</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>"DCF" or "Direct".</summary>
    public string TemplateType { get; private set; } = null!;

    public string? Description { get; private set; }

    public int TotalNumberOfYears { get; private set; }

    public int TotalNumberOfDayInYear { get; private set; } = 365;

    /// <summary>Capitalize rate as a percentage, e.g. 3.0 = 3%.</summary>
    public decimal CapitalizeRate { get; private set; }

    /// <summary>Discount rate as a percentage, e.g. 5.0 = 5%. Zero for Direct templates.</summary>
    public decimal DiscountedRate { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int DisplaySeq { get; private set; }

    private PricingTemplate()
    {
        // For EF Core
    }

    public static PricingTemplate Create(
        Guid id,
        string code,
        string name,
        string templateType,
        string? description,
        int totalNumberOfYears,
        int totalNumberOfDayInYear,
        decimal capitalizeRate,
        decimal discountedRate,
        bool isActive,
        int displaySeq)
    {
        return new PricingTemplate
        {
            Id = id,
            Code = code,
            Name = name,
            TemplateType = templateType,
            Description = description,
            TotalNumberOfYears = totalNumberOfYears,
            TotalNumberOfDayInYear = totalNumberOfDayInYear,
            CapitalizeRate = capitalizeRate,
            DiscountedRate = discountedRate,
            IsActive = isActive,
            DisplaySeq = displaySeq
        };
    }

    public void AddSection(PricingTemplateSection section)
    {
        _sections.Add(section);
    }
}
