namespace Request.Domain.RequestTitles;

public class MachineInfo : ValueObject
{
    public bool RegistrationStatus { get; }
    public string? RegistrationNo { get; }
    public string? MachineType { get; }
    public string? InstallationStatus { get; }
    public string? InvoiceNumber { get; }
    public int? NumberOfMachinery { get; }

    private MachineInfo()
    {
        // For EF Core
    }

    private MachineInfo(
        bool registrationStatus,
        string? registrationNo,
        string? machineType,
        string? installationStatus,
        string? invoiceNumber,
        int? numberOfMachinery
    )
    {
        RegistrationStatus = registrationStatus;
        RegistrationNo = registrationNo;
        MachineType = machineType;
        InstallationStatus = installationStatus;
        InvoiceNumber = invoiceNumber;
        NumberOfMachinery = numberOfMachinery;
    }

    public static MachineInfo Create(
        bool registrationStatus,
        string? registrationNo,
        string? machineryType,
        string? installationStatus,
        string? invoiceNumber,
        int? numberOfMachinery
    )
    {
        return new MachineInfo(
            registrationStatus,
            registrationNo,
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
        if (NumberOfMachinery is null || NumberOfMachinery < 0)
            throw new ArgumentException("numberOfMachinery must be >= 0.");
        switch (RegistrationStatus)
        {
            case true when string.IsNullOrWhiteSpace(RegistrationNo):
                throw new ArgumentException("registrationNo is required when registrationStatus is true.");
            case false when string.IsNullOrWhiteSpace(InvoiceNumber):
                throw new ArgumentException("invoiceNumber is required when registrationStatus is false.");
        }
    }
}