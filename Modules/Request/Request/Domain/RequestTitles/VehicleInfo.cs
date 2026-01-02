namespace Request.Domain.RequestTitles;

public class VehicleInfo : ValueObject
{
    public string? VehicleType { get; }
    public string? VehicleLocation { get; }
    public string? VIN { get; }
    public string? LicensePlateNumber { get; }

    private VehicleInfo()
    {
        // For EF Core
    }

    private VehicleInfo(
        string? vehicleType,
        string? vehicleLocation,
        string? vin,
        string? licensePlateNumber
    )
    {
        VehicleType = vehicleType;
        VehicleLocation = vehicleLocation;
        VIN = vin;
        LicensePlateNumber = licensePlateNumber;
    }

    public static VehicleInfo Create(
        string? vehicleType,
        string? vehicleLocation,
        string? vin,
        string? licensePlateNumber
    )
    {
        return new VehicleInfo(
            vehicleType,
            vehicleLocation,
            vin,
            licensePlateNumber
        );
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(VIN);
    }
}