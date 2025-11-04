namespace Request.Requests.ValueObjects;

public class LoanDetail : ValueObject
{
    public string? LoanApplicationNo { get; }
    public decimal? FacilityLimit { get; }
    public decimal? TopUpLimit { get; }
    public decimal? OldFacilityLimit { get; }
    public decimal? TotalSellingPrice { get; }

    private LoanDetail(
        string? loanApplicationNo, 
        decimal? facilityLimit, 
        decimal? topUpLimit,
        decimal? oldFacilityLimit,
        decimal? totalSellingPrice
    )
    {
        LoanApplicationNo = loanApplicationNo;
        FacilityLimit = facilityLimit;
        TopUpLimit = topUpLimit;
        OldFacilityLimit = oldFacilityLimit;
        TotalSellingPrice = totalSellingPrice;
    }

    public static LoanDetail Create(
        string? loanApplicationNo, 
        decimal? facilityLimit, 
        decimal? topUpLimit, 
        decimal? oldFacilityLimit, 
        decimal? totalSellingPrice
    )
    {
        return new LoanDetail(
            loanApplicationNo, 
            facilityLimit, 
            topUpLimit, 
            oldFacilityLimit, 
            totalSellingPrice
        );
        
    }
}