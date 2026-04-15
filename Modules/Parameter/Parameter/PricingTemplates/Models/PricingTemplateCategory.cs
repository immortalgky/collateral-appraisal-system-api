namespace Parameter.PricingTemplates.Models;

/// <summary>
/// A category grouping within a pricing template section
/// (e.g. "Operating Income", "Direct Operating Expenses", "Fixed Charge").
/// Blueprint only — no computed totals.
/// </summary>
public class PricingTemplateCategory : Entity<Guid>
{
    private readonly List<PricingTemplateAssumption> _assumptions = [];

    public IReadOnlyCollection<PricingTemplateAssumption> Assumptions => _assumptions.AsReadOnly();

    public Guid PricingTemplateSectionId { get; private set; }

    public string CategoryType { get; private set; } = null!;

    public string CategoryName { get; private set; } = null!;

    /// <summary>"positive" | "negative" | "gop" | "fixedExps" | "empty"</summary>
    public string Identifier { get; private set; } = null!;

    public int DisplaySeq { get; private set; }

    private PricingTemplateCategory()
    {
        // For EF Core
    }

    public static PricingTemplateCategory Create(
        Guid id,
        Guid pricingTemplateSectionId,
        string categoryType,
        string categoryName,
        string identifier,
        int displaySeq)
    {
        return new PricingTemplateCategory
        {
            Id = id,
            PricingTemplateSectionId = pricingTemplateSectionId,
            CategoryType = categoryType,
            CategoryName = categoryName,
            Identifier = identifier,
            DisplaySeq = displaySeq
        };
    }

    public void AddAssumption(PricingTemplateAssumption assumption)
    {
        _assumptions.Add(assumption);
    }
}
