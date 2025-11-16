namespace Request.RequestTitles.ValueObjects;

public class MachineInfo : ValueObject
{
    public string? MachineStatus { get; }
    public string? MachineType { get; }
    public string? InstallationStatus { get; }
    public string? InvoiceNumber { get; }
    public int? NumberOfMachinery { get; }

    private MachineInfo()
    {
        // For EF Core
    }

    private MachineInfo(
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

    public static MachineInfo Create(
        string? machineryStatus, 
        string? machineryType, 
        string? installationStatus, 
        string? invoiceNumber, 
        int? numberOfMachinery
    )
    {
        return new MachineInfo(
            machineryStatus, 
            machineryType, 
            installationStatus, 
            invoiceNumber, 
            numberOfMachinery
        );
    }
}