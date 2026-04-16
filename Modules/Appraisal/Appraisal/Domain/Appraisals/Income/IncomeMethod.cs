namespace Appraisal.Domain.Appraisals.Income;

/// <summary>
/// Owned entity on IncomeAssumption.
/// Stores the method type code (01–14) and the polymorphic detail JSON,
/// plus the year-indexed computed values array.
/// </summary>
public class IncomeMethod
{
    /// <summary>Method type code: "01" through "14".</summary>
    public string MethodTypeCode { get; private set; } = null!;

    /// <summary>Polymorphic JSON — one of the 14 Method*Detail shapes.</summary>
    public string DetailJson { get; private set; } = "{}";

    /// <summary>Year-indexed decimal array serialized as JSON (server-computed).</summary>
    public string TotalMethodValuesJson { get; private set; } = "[]";

    private IncomeMethod()
    {
        // For EF Core owned entity
    }

    public static IncomeMethod Create(string methodTypeCode, string detailJson)
    {
        if (string.IsNullOrWhiteSpace(methodTypeCode))
            throw new ArgumentException("MethodTypeCode is required", nameof(methodTypeCode));

        return new IncomeMethod
        {
            MethodTypeCode = methodTypeCode,
            DetailJson = detailJson
        };
    }

    public void SetDetail(string methodTypeCode, string detailJson)
    {
        MethodTypeCode = methodTypeCode;
        DetailJson = detailJson;
    }

    public void SetComputedValues(string totalMethodValuesJson)
    {
        TotalMethodValuesJson = totalMethodValuesJson;
    }

    /// <summary>
    /// Replaces the detail JSON for this method (e.g. after a ref-target ID rewrite on initialize).
    /// Does not change MethodTypeCode.
    /// </summary>
    public void SetDetailJson(string detailJson)
    {
        DetailJson = detailJson;
    }
}
