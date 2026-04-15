namespace Appraisal.Domain.Appraisals.Income;

/// <summary>
/// A single assumption line within an IncomeCategory.
/// Holds the calculation method (one of 14 types) and year-indexed computed totals.
/// </summary>
public class IncomeAssumption : Entity<Guid>
{
    public Guid IncomeCategoryId { get; private set; }

    /// <summary>Assumption type code, e.g. "I00", "I01", "E15", "M99".</summary>
    public string AssumptionType { get; private set; } = null!;

    public string AssumptionName { get; private set; } = null!;

    /// <summary>"positive" | "negative" | "empty"</summary>
    public string Identifier { get; private set; } = null!;

    public int DisplaySeq { get; private set; }

    /// <summary>Year-indexed decimal array serialized as JSON (server-computed).</summary>
    public string TotalAssumptionValuesJson { get; private set; } = "[]";

    // Owned — 1:1
    public IncomeMethod Method { get; private set; } = null!;

    private IncomeAssumption()
    {
        // For EF Core
    }

    public static IncomeAssumption Create(
        Guid incomeCategoryId,
        string assumptionType,
        string assumptionName,
        string identifier,
        int displaySeq,
        string methodTypeCode,
        string detailJson)
    {
        return new IncomeAssumption
        {
            //Id = Guid.CreateVersion7(),
            IncomeCategoryId = incomeCategoryId,
            AssumptionType = assumptionType,
            AssumptionName = assumptionName,
            Identifier = identifier,
            DisplaySeq = displaySeq,
            Method = IncomeMethod.Create(methodTypeCode, detailJson)
        };
    }

    public void Update(
        string assumptionType,
        string assumptionName,
        string identifier,
        int displaySeq,
        string methodTypeCode,
        string detailJson)
    {
        AssumptionType = assumptionType;
        AssumptionName = assumptionName;
        Identifier = identifier;
        DisplaySeq = displaySeq;
        Method.SetDetail(methodTypeCode, detailJson);
    }

    public void SetComputedValues(string totalAssumptionValuesJson, string totalMethodValuesJson)
    {
        TotalAssumptionValuesJson = totalAssumptionValuesJson;
        Method.SetComputedValues(totalMethodValuesJson);
    }
}
