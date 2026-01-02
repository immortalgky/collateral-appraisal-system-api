namespace Request.Domain.Requests;

public class LoanDetail : ValueObject
{
    public string? BankingSegment { get; }
    public string? LoanApplicationNumber { get; }
    public decimal? FacilityLimit { get; }
    public decimal? AdditionalFacilityLimit { get; }
    public decimal? PreviousFacilityLimit { get; }
    public decimal? TotalSellingPrice { get; }

    private LoanDetail()
    {
        // For EF Core
    }

    private LoanDetail(LoanDetailData data)
    {
        BankingSegment = data.BankingSegment;
        LoanApplicationNumber = data.LoanApplicationNumber;
        FacilityLimit = data.FacilityLimit;
        AdditionalFacilityLimit = data.AdditionalFacilityLimit;
        PreviousFacilityLimit = data.PreviousFacilityLimit;
        TotalSellingPrice = data.TotalSellingPrice;
    }

    public static LoanDetail Create(LoanDetailData data)
    {
        return new LoanDetail(data);
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(BankingSegment);
        ArgumentException.ThrowIfNullOrWhiteSpace(LoanApplicationNumber);
        if (FacilityLimit is null || FacilityLimit <= 0)
            throw new ArgumentException("FacilityLimit is required or must be greater than zero.");
        if (AdditionalFacilityLimit is not null && (PreviousFacilityLimit is null || PreviousFacilityLimit <= 0))
            throw new ArgumentException("AdditionalFacilityLimit is required or must be greater than zero.");
        if (TotalSellingPrice is null || TotalSellingPrice <= 0)
            throw new ArgumentException("TotalSellingPrice is required or must be greater than zero.");
    }
}

public record LoanDetailData(
    string? BankingSegment,
    string? LoanApplicationNumber,
    decimal? FacilityLimit,
    decimal? AdditionalFacilityLimit,
    decimal? PreviousFacilityLimit,
    decimal? TotalSellingPrice
);