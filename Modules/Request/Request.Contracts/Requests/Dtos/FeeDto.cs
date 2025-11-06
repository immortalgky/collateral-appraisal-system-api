namespace Request.Contracts.Requests.Dtos;

public record FeeDto(
    string? FeePaymentType,
    decimal? AbsorbedFee,
    string? FeeNotes
);