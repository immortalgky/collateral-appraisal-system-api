namespace Request.Contracts.Requests.Dtos;

public record FeeDto(
    string? FeePaymentType,
    string? FeeNotes,
    decimal? AbsorbedAmount
);