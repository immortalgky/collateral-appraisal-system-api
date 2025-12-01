namespace Request.Contracts.Requests.Dtos;

public record FeeDto(
    string FeeType,
    string? FeeNote,
    decimal? BankAbsorbAmt
);