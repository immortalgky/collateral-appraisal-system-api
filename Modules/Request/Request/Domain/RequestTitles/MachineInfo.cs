namespace Request.Domain.RequestTitles;

public class MachineInfo : ValueObject
{
    public bool RegistrationStatus { get; }
    public string? RegistrationNumber { get; }
    public string? MachineType { get; }
    public string? InstallationStatus { get; }
    public string? InvoiceNumber { get; }
    public int? NumberOfMachine { get; }

    private MachineInfo()
    {
        // For EF Core
    }

    private MachineInfo(
        bool registrationStatus,
        string? registrationNumber,
        string? machineType,
        string? installationStatus,
        string? invoiceNumber,
        int? numberOfMachine
    )
    {
        RegistrationStatus = registrationStatus;
        RegistrationNumber = registrationNumber;
        MachineType = machineType;
        InstallationStatus = installationStatus;
        InvoiceNumber = invoiceNumber;
        NumberOfMachine = numberOfMachine;
    }

    public static MachineInfo Create(
        bool registrationStatus,
        string? registrationNumber,
        string? machineryType,
        string? installationStatus,
        string? invoiceNumber,
        int? numberOfMachinery
    )
    {
        return new MachineInfo(
            registrationStatus,
            registrationNumber,
            machineryType,
            installationStatus,
            invoiceNumber,
            numberOfMachinery
        );
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(MachineType);
        ArgumentException.ThrowIfNullOrWhiteSpace(InstallationStatus);
        if (NumberOfMachine is null || NumberOfMachine < 0)
            throw new ArgumentException("numberOfMachinery must be >= 0.");

        switch (RegistrationStatus)
        {
            case true when string.IsNullOrWhiteSpace(RegistrationNumber):
                throw new ArgumentException("registrationNo is required when registrationStatus is true.");
            case false when string.IsNullOrWhiteSpace(InvoiceNumber):
                throw new ArgumentException("invoiceNumber is required when registrationStatus is false.");
        }
    }
}