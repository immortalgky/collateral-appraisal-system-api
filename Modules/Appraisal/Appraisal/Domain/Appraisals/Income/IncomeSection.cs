namespace Appraisal.Domain.Appraisals.Income;

/// <summary>
/// A top-level section in IncomeAnalysis (e.g. "Income", "Expenses", "DCF Summary").
/// SectionType: "income" | "expenses" | "summaryDCF".
/// summaryDCF sections have no categories — summary data lives in IncomeSummary on the root.
/// </summary>
public class IncomeSection : Entity<Guid>
{
    private readonly List<IncomeCategory> _categories = [];

    public IReadOnlyCollection<IncomeCategory> Categories => _categories.AsReadOnly();

    public Guid IncomeAnalysisId { get; private set; }

    /// <summary>"income" | "expenses" | "summaryDCF"</summary>
    public string SectionType { get; private set; } = null!;

    public string SectionName { get; private set; } = null!;

    /// <summary>"positive" | "negative" | "empty"</summary>
    public string Identifier { get; private set; } = null!;

    public int DisplaySeq { get; private set; }

    /// <summary>Year-indexed decimal array serialized as JSON (server-computed).</summary>
    public string TotalSectionValuesJson { get; private set; } = "[]";

    private IncomeSection()
    {
        // For EF Core
    }

    public static IncomeSection Create(
        Guid incomeAnalysisId,
        string sectionType,
        string sectionName,
        string identifier,
        int displaySeq)
    {
        return new IncomeSection
        {
            //Id = Guid.CreateVersion7(),
            IncomeAnalysisId = incomeAnalysisId,
            SectionType = sectionType,
            SectionName = sectionName,
            Identifier = identifier,
            DisplaySeq = displaySeq
        };
    }

    public void Update(
        string sectionType,
        string sectionName,
        string identifier,
        int displaySeq)
    {
        SectionType = sectionType;
        SectionName = sectionName;
        Identifier = identifier;
        DisplaySeq = displaySeq;
    }

    public void SetComputedValues(string totalSectionValuesJson)
    {
        TotalSectionValuesJson = totalSectionValuesJson;
    }

    public IncomeCategory AddCategory(
        string categoryType,
        string categoryName,
        string identifier,
        int displaySeq)
    {
        var category = IncomeCategory.Create(Id, categoryType, categoryName, identifier, displaySeq);
        _categories.Add(category);
        return category;
    }

    public void ReplaceCategories(IEnumerable<IncomeCategory> categories)
    {
        _categories.Clear();
        _categories.AddRange(categories);
    }
}
