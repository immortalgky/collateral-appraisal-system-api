namespace Request.Contracts.Requests.Dtos;

public record LoanDetailDto(
    string? BankingSegment,
    string? LoanApplicationNumber,
    decimal? FacilityLimit,
    decimal? AdditionalFacilityLimit,
    decimal? PreviousFacilityLimit,
    decimal? TotalSellingPrice
);