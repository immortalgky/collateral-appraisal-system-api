namespace Request.Contracts.Requests.Dtos;

public record MachineryDto(
    string? MachineryStatus,
    string? MachineryType,
    string? InstallationStatus,
    string? InvoiceNumber,
    int? NumberOfMachinery
);