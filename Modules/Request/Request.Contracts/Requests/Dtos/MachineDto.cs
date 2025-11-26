namespace Request.Contracts.Requests.Dtos;

public record MachineDto(
    string? MachineStatus,
    string? MachineType,
    string? InstallationStatus,
    string? InvoiceNumber,
    int? NumberOfMachine
);