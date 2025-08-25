namespace Shared.Dtos;
public record MachineDto(
    string? MachineStatus,
    string? MachineType,
    string? MachineRegistrationStatus,
    string? MachineRegistrationNo,
    string? MachineInvoiceNo,
    int? NoOfMachine
);
