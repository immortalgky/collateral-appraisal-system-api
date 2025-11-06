namespace Request.Contracts.Requests.Dtos;

public record LoanDetailDto(
    string? BankingSegment,
    string? LoanApplicationNo,
    decimal? FacilityLimit,
    decimal? TopUpLimit,
    decimal? OldFacilityLimit,
    decimal? TotalSellingPrice
);