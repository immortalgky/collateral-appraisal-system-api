namespace Request.Domain.RequestTitles;

public class VesselInfo : ValueObject
{
    public string? VesselType { get; }
    public string? VesselLocation { get; }
    public string? HIN { get; }
    public string? VesselRegistrationNumber { get; }

    private VesselInfo()
    {
        // For EF Core
    }

    private VesselInfo(
        string? vesselType,
        string? vesselLocation,
        string? hin,
        string? vesselRegistrationNumber
    )
    {
        VesselType = vesselType;
        VesselLocation = vesselLocation;
        HIN = hin;
        VesselRegistrationNumber = vesselRegistrationNumber;
    }

    public static VesselInfo Create(
        string? vesselType,
        string? vesselLocation,
        string? hullIdentificationNumber,
        string? vesselRegistrationNumber
    )
    {
        return new VesselInfo(
            vesselType,
            vesselLocation,
            hullIdentificationNumber,
            vesselRegistrationNumber
        );
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(HIN);
    }
}
