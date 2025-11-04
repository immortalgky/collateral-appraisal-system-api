namespace Request.Requests.ValueObjects;

public class LoanDetail : ValueObject
{
    public string? LoanApplicationNo { get; }
    public decimal? LimitAmt { get; } // FacilityLimit
    // ADD: TopUpLimit
    // ADD: OldFacilityLimit
    public decimal? TotalSellingPrice { get; }

    private LoanDetail(string? loanApplicationNo, decimal? limitAmt, decimal? totalSellingPrice)
    {
        LoanApplicationNo = loanApplicationNo;
        LimitAmt = limitAmt;
        TotalSellingPrice = totalSellingPrice;
    }

    public static LoanDetail Create(string? loanApplicationNo, decimal? limitAmt, decimal? totalSellingPrice)
    {
        return new LoanDetail(loanApplicationNo, limitAmt, totalSellingPrice);
    }
}