namespace Request.Requests.ValueObjects;

public class LoanDetail : ValueObject
{
    public string? LoanApplicationNo { get; }
    public string? BankingSegment { get; }
    public decimal? FacilityLimit { get; }
    public decimal? AdditionalFacilityLimit { get; }
    public decimal? PreviousFacilityLimit { get; }
    public decimal? TotalSellingPrice { get; }

    private LoanDetail(string? loanApplicationNo, string? bankingSegment, decimal? facilityLimit,
        decimal? additionalFacilityLimit, decimal? previousFacilityLimit,
        decimal? totalSellingPrice)
    {
        LoanApplicationNo = loanApplicationNo;
        BankingSegment = bankingSegment;
        FacilityLimit = facilityLimit;
        AdditionalFacilityLimit = additionalFacilityLimit;
        PreviousFacilityLimit = previousFacilityLimit;
        TotalSellingPrice = totalSellingPrice;
    }

    public static LoanDetail Create(string? loanApplicationNo, string? bankingSegment, decimal? facilityLimit,
        decimal? additionalFacilityLimit, decimal? previousFacilityLimit,
        decimal? totalSellingPrice)
    {
        return new LoanDetail(loanApplicationNo, bankingSegment, facilityLimit, additionalFacilityLimit,
            previousFacilityLimit, totalSellingPrice);
    }
}