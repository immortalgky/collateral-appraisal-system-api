namespace Parameter.PricingTemplates.Models;

/// <summary>
/// A single assumption line within a pricing template category.
/// Stores the default method type and its default detail payload as JSON —
/// this is the blueprint that gets cloned into IncomeAssumption on initialization.
/// </summary>
public class PricingTemplateAssumption : Entity<Guid>
{
    public Guid PricingTemplateCategoryId { get; private set; }

    /// <summary>Assumption type code, e.g. "I00", "E15", "M99".</summary>
    public string AssumptionType { get; private set; } = null!;

    public string AssumptionName { get; private set; } = null!;

    /// <summary>"positive" | "negative" | "empty"</summary>
    public string Identifier { get; private set; } = null!;

    public int DisplaySeq { get; private set; }

    /// <summary>Default method type code, e.g. "01"–"14".</summary>
    public string MethodTypeCode { get; private set; } = null!;

    /// <summary>
    /// Default method detail as a JSON string (camelCase).
    /// Shape varies per method type — stored verbatim and passed through to the frontend.
    /// </summary>
    public string MethodDetailJson { get; private set; } = "{}";

    private PricingTemplateAssumption()
    {
        // For EF Core
    }

    public static PricingTemplateAssumption Create(
        Guid id,
        Guid pricingTemplateCategoryId,
        string assumptionType,
        string assumptionName,
        string identifier,
        int displaySeq,
        string methodTypeCode,
        string methodDetailJson)
    {
        return new PricingTemplateAssumption
        {
            Id = id,
            PricingTemplateCategoryId = pricingTemplateCategoryId,
            AssumptionType = assumptionType,
            AssumptionName = assumptionName,
            Identifier = identifier,
            DisplaySeq = displaySeq,
            MethodTypeCode = methodTypeCode,
            MethodDetailJson = methodDetailJson
        };
    }
}
