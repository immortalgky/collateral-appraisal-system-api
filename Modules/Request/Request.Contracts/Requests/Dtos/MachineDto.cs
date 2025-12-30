namespace Request.Contracts.Requests.Dtos;

public record MachineDto(
    bool registrationStatus,
    string? registrationNo,
    string? MachineType,
    string? InstallationStatus,
    string? InvoiceNumber,
    int? NumberOfMachine
);