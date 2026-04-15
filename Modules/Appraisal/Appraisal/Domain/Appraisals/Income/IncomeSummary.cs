namespace Appraisal.Domain.Appraisals.Income;

/// <summary>
/// Owned value object on IncomeAnalysis.
/// Stores year-indexed summary arrays as JSON — all server-computed.
/// Each field is a JSON-encoded decimal[] indexed by year (0 = year 1).
/// </summary>
public class IncomeSummary
{
    public string ContractRentalFeeJson { get; private set; } = "[]";
    public string GrossRevenueJson { get; private set; } = "[]";
    public string GrossRevenueProportionalJson { get; private set; } = "[]";
    public string TerminalRevenueJson { get; private set; } = "[]";
    public string TotalNetJson { get; private set; } = "[]";
    public string DiscountJson { get; private set; } = "[]";
    public string PresentValueJson { get; private set; } = "[]";

    private IncomeSummary()
    {
        // For EF Core owned entity
    }

    public static IncomeSummary Empty() => new();

    public static IncomeSummary Create(
        string contractRentalFeeJson,
        string grossRevenueJson,
        string grossRevenueProportionalJson,
        string terminalRevenueJson,
        string totalNetJson,
        string discountJson,
        string presentValueJson)
    {
        return new IncomeSummary
        {
            ContractRentalFeeJson = contractRentalFeeJson,
            GrossRevenueJson = grossRevenueJson,
            GrossRevenueProportionalJson = grossRevenueProportionalJson,
            TerminalRevenueJson = terminalRevenueJson,
            TotalNetJson = totalNetJson,
            DiscountJson = discountJson,
            PresentValueJson = presentValueJson
        };
    }
}
