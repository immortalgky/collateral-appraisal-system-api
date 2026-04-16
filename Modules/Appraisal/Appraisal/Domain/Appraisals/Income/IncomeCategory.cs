namespace Appraisal.Domain.Appraisals.Income;

/// <summary>
/// A category within an IncomeSection (e.g. "Room Revenue", "Fixed Expenses").
/// CategoryType: "income" | "expenses" | "gop" | "fixedExps".
/// gop categories may have an empty Assumptions collection.
/// </summary>
public class IncomeCategory : Entity<Guid>
{
    private readonly List<IncomeAssumption> _assumptions = [];

    public IReadOnlyCollection<IncomeAssumption> Assumptions => _assumptions.AsReadOnly();

    public Guid IncomeSectionId { get; private set; }

    /// <summary>"income" | "expenses" | "gop" | "fixedExps"</summary>
    public string CategoryType { get; private set; } = null!;

    public string CategoryName { get; private set; } = null!;

    /// <summary>"positive" | "negative" | "empty"</summary>
    public string Identifier { get; private set; } = null!;

    public int DisplaySeq { get; private set; }

    /// <summary>Year-indexed decimal array serialized as JSON (server-computed).</summary>
    public string TotalCategoryValuesJson { get; private set; } = "[]";

    private IncomeCategory()
    {
        // For EF Core
    }

    public static IncomeCategory Create(
        Guid incomeSectionId,
        string categoryType,
        string categoryName,
        string identifier,
        int displaySeq,
        // Preview handler passes a pre-assigned Guid so the in-memory graph has real Ids
        // without going through EF. Save path omits this parameter; EF assigns via NEWSEQUENTIALID().
        Guid? id = null)
    {
        var entity = new IncomeCategory
        {
            // Id intentionally omitted — EF assigns it via HasDefaultValueSql("NEWSEQUENTIALID()") on insert.
            IncomeSectionId = incomeSectionId,
            CategoryType = categoryType,
            CategoryName = categoryName,
            Identifier = identifier,
            DisplaySeq = displaySeq
        };

        if (id.HasValue)
            entity.Id = id.Value;

        return entity;
    }

    public void Update(
        string categoryType,
        string categoryName,
        string identifier,
        int displaySeq)
    {
        CategoryType = categoryType;
        CategoryName = categoryName;
        Identifier = identifier;
        DisplaySeq = displaySeq;
    }

    public void SetComputedValues(string totalCategoryValuesJson)
    {
        TotalCategoryValuesJson = totalCategoryValuesJson;
    }

    public IncomeAssumption AddAssumption(
        string assumptionType,
        string assumptionName,
        string identifier,
        int displaySeq,
        string methodTypeCode,
        string detailJson)
    {
        var assumption = IncomeAssumption.Create(
            Id, assumptionType, assumptionName, identifier, displaySeq, methodTypeCode, detailJson);
        _assumptions.Add(assumption);
        return assumption;
    }

    public void ReplaceAssumptions(IEnumerable<IncomeAssumption> assumptions)
    {
        _assumptions.Clear();
        _assumptions.AddRange(assumptions);
    }

    public void AttachAssumption(IncomeAssumption assumption) => _assumptions.Add(assumption);

    public void RemoveAssumption(IncomeAssumption assumption) => _assumptions.Remove(assumption);
}
