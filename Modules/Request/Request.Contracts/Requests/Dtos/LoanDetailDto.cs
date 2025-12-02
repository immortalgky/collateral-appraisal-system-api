namespace Request.Contracts.Requests.Dtos;

public record LoanDetailDto(
    string? BankingSegment,
    string? LoanApplicationNo,
    decimal? FacilityLimit,
    decimal? AdditionalFacilityLimit,
    decimal? PreviousFacilityLimit,
    decimal? TotalSellingPrice
);