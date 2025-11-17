namespace Request.Contracts.Requests.Dtos;

public record LoanDetailDto(
    string? LoanApplicationNo,
    string? BankingSegment,
    decimal? FacilityLimit,
    decimal? AdditionalFacilityLimit,
    decimal? PreviousFacilityLimit,
    decimal? TotalSellingPrice
);