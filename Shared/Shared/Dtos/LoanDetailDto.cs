namespace Shared.Dtos;

public record LoanDetailDto(
    string? LoanApplicationNo,
    decimal? LimitAmt,
    decimal? TotalSellingPrice
);