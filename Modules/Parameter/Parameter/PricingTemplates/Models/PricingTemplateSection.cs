namespace Parameter.PricingTemplates.Models;

/// <summary>
/// A top-level section within a pricing template (e.g. "income", "expenses", "summaryDCF").
/// Blueprint only — no computed totals.
/// </summary>
public class PricingTemplateSection : Entity<Guid>
{
    private readonly List<PricingTemplateCategory> _categories = [];

    public IReadOnlyCollection<PricingTemplateCategory> Categories => _categories.AsReadOnly();

    public Guid PricingTemplateId { get; private set; }

    public string SectionType { get; private set; } = null!;

    public string SectionName { get; private set; } = null!;

    /// <summary>"positive" | "negative" | "empty"</summary>
    public string Identifier { get; private set; } = null!;

    public int DisplaySeq { get; private set; }

    private PricingTemplateSection()
    {
        // For EF Core
    }

    public static PricingTemplateSection Create(
        Guid id,
        Guid pricingTemplateId,
        string sectionType,
        string sectionName,
        string identifier,
        int displaySeq)
    {
        return new PricingTemplateSection
        {
            Id = id,
            PricingTemplateId = pricingTemplateId,
            SectionType = sectionType,
            SectionName = sectionName,
            Identifier = identifier,
            DisplaySeq = displaySeq
        };
    }

    public void AddCategory(PricingTemplateCategory category)
    {
        _categories.Add(category);
    }
}
