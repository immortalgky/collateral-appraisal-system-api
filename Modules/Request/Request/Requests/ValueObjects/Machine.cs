namespace Request.Requests.ValueObjects;

public class Machine : ValueObject
{
    private Machine(string? machineStatus, string? machineType, string? machineRegistrationStatus,
        string? machineRegistrationNo, string? machineInvoiceNo, decimal? noOfMachine)
    {
        MachineStatus = machineStatus;
        MachineType = machineType;
        MachineRegistrationStatus = machineRegistrationStatus;
        MachineRegistrationNo = machineRegistrationNo;
        MachineInvoiceNo = machineInvoiceNo;
        NoOfMachine = noOfMachine;
    }
    public string? MachineStatus { get; } = default!;
    public string? MachineType { get; } = default!;
    public string? MachineRegistrationStatus { get; } = default!;
    public string? MachineRegistrationNo { get; } = default!;
    public string? MachineInvoiceNo { get; } = default!;
    public decimal? NoOfMachine { get; }

    public static Machine Create(string? machineStatus, string? machineType, string? machineRegistrationStatus,
        string? machineRegistrationNo, string? machineInvoiceNo, decimal? noOfMachine)
    {
        return new Machine(machineStatus, machineType, machineRegistrationStatus, machineRegistrationNo,
            machineInvoiceNo, noOfMachine);
    }
}