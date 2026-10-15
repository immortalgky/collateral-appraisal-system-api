namespace Request.Domain.RequestTitles;

public class VehicleInfo : ValueObject
{
    public string? VehicleType { get; }
    public string? VehicleRegistrationNumber { get; }
    public string? VehicleLocation { get; }
    public string? VIN { get; }
    public string? LicensePlateNumber { get; }

    private VehicleInfo()
    {
        // For EF Core
    }

    private VehicleInfo(
        string? vehicleType,
        string? vehicleRegistrationNumber,
        string? vehicleLocation,
        string? vin,
        string? licensePlateNumber
    )
    {
        VehicleType = vehicleType;
        VehicleRegistrationNumber = vehicleRegistrationNumber;
        VehicleLocation = vehicleLocation;
        VIN = vin;
        LicensePlateNumber = licensePlateNumber;
    }

    public static VehicleInfo Create(
        string? vehicleType,
        string? vehicleRegistrationNumber,
        string? vehicleLocation,
        string? vin,
        string? licensePlateNumber
    )
    {
        return new VehicleInfo(
            vehicleType,
            vehicleRegistrationNumber,
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