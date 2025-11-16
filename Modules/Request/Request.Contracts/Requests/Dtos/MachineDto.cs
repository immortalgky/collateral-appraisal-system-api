namespace Request.Contracts.Requests.Dtos;

public record MachineDto(
    string? MachineryStatus,
    string? MachineryType,
    string? InstallationStatus,
    string? InvoiceNumber,
    int? NumberOfMachinery
);