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
        int displaySeq,
        // Preview handler passes a pre-assigned Guid so the in-memory graph has real Ids
        // without going through EF. Save path omits this parameter; EF assigns via NEWSEQUENTIALID().
        Guid? id = null)
    {
        var entity = new IncomeSection
        {
            // Id intentionally omitted — EF assigns it via HasDefaultValueSql("NEWSEQUENTIALID()") on insert.
            IncomeAnalysisId = incomeAnalysisId,
            SectionType = sectionType,
            SectionName = sectionName,
            Identifier = identifier,
            DisplaySeq = displaySeq
        };

        if (id.HasValue)
            entity.Id = id.Value;

        return entity;
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

    public void AttachCategory(IncomeCategory category) => _categories.Add(category);

    public void RemoveCategory(IncomeCategory category) => _categories.Remove(category);
}
