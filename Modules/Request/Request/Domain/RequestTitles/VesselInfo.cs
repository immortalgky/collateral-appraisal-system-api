namespace Request.Domain.RequestTitles;

public class VesselInfo : ValueObject
{
    public string? VesselType { get; }
    public string? VesselLocation { get; }
    public string? HullIdentificationNumber { get; }
    public string? VesselRegistrationNumber { get; }

    private VesselInfo()
    {
        // For EF Core
    }

    private VesselInfo(
        string? vesselType,
        string? vesselLocation,
        string? hullIdentificationNumber,
        string? vesselRegistrationNumber
    )
    {
        VesselType = vesselType;
        VesselLocation = vesselLocation;
        HullIdentificationNumber = hullIdentificationNumber;
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
        ArgumentException.ThrowIfNullOrWhiteSpace(HullIdentificationNumber);
    }
}
