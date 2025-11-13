namespace Request.RequestTitles.ValueObjects;

public class Machinery : ValueObject
{
    public string? MachineryStatus { get; }
    public string? MachineryType { get; }
    public string? InstallationStatus { get; }
    public string? InvoiceNumber { get; }
    public int? NumberOfMachinery { get; }

    private Machinery()
    {
        // For EF Core
    }

    private Machinery(
        string? machineryStatus, 
        string? machineryType, 
        string? installationStatus, 
        string? invoiceNumber, 
        int? numberOfMachinery
    )
    {
        MachineryStatus = machineryStatus;
        MachineryType = machineryType;
        InstallationStatus = installationStatus;
        InvoiceNumber = invoiceNumber;
        NumberOfMachinery = numberOfMachinery;
    }

    public static Machinery Create(
        string? machineryStatus, 
        string? machineryType, 
        string? installationStatus, 
        string? invoiceNumber, 
        int? numberOfMachinery
    )
    {
        return new Machinery(
            machineryStatus, 
            machineryType, 
            installationStatus, 
            invoiceNumber, 
            numberOfMachinery
        );
    }
}