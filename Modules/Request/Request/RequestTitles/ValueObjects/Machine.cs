namespace Request.RequestTitles.ValueObjects;

public class Machine : ValueObject
{
    public string? MachineStatus { get; }
    public string? MachineType { get; }
    public string? MachineRegistrationStatus { get; }
    public string? MachineRegistrationNo { get; }
    public string? MachineInvoiceNo { get; }
    public int? NoOfMachine { get; }

    private Machine()
    {
        // For EF Core
    }

    private Machine(string? machineStatus, string? machineType, string? machineRegistrationStatus,
        string? machineRegistrationNo, string? machineInvoiceNo, int? noOfMachine)
    {
        MachineStatus = machineStatus;
        MachineType = machineType;
        MachineRegistrationStatus = machineRegistrationStatus;
        MachineRegistrationNo = machineRegistrationNo;
        MachineInvoiceNo = machineInvoiceNo;
        NoOfMachine = noOfMachine;
    }

    public static Machine Create(string? machineStatus, string? machineType, string? machineRegistrationStatus,
        string? machineRegistrationNo, string? machineInvoiceNo, int? noOfMachine)
    {
        return new Machine(machineStatus, machineType, machineRegistrationStatus, machineRegistrationNo,
            machineInvoiceNo, noOfMachine);
    }
}