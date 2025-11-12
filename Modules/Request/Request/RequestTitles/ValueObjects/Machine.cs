namespace Request.RequestTitles.ValueObjects;

public class Machine : ValueObject
{
    public string? MachineStatus { get; }
    public string? MachineType { get; }
    public string? InstallationStatus { get; }
    public string? InvoiceNumber { get; }
    public int? NumberOfMachinery { get; }

    private Machine()
    {
        // For EF Core
    }

    private Machine(
        string? machineStatus, 
        string? machineType, 
        string? installationStatus, 
        string? invoiceNumber, 
        int? numberOfMachinery
    )
    {
        MachineStatus = machineStatus;
        MachineType = machineType;
        InstallationStatus = installationStatus;
        InvoiceNumber = invoiceNumber;
        NumberOfMachinery = numberOfMachinery;
    }

    public static Machine Create(
        string? machineStatus, 
        string? machineType, 
        string? installationStatus, 
        string? invoiceNumber, 
        int? numberOfMachinery
    )
    {
        return new Machine(
            machineStatus, 
            machineType, 
            installationStatus, 
            invoiceNumber, 
            numberOfMachinery
        );
    }
}