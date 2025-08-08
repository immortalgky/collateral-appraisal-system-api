namespace Request.RequestTitles.ValueObjects;

public class Vehicle : ValueObject
{
    private Vehicle(string? vehicleType, string? vehicleRegistrationNo, string? vehicleLocation)
    {
        VehicleType = vehicleType;
        VehicleRegistrationNo = vehicleRegistrationNo;
        VehicleLocation = vehicleLocation;
    }

    public string? VehicleType { get; }
    public string? VehicleRegistrationNo { get; }
    public string? VehicleLocation { get; }

    public static Vehicle Create(string? vehicleType, string? vehicleRegistrationNo, string? vehicleLocation)
    {
        return new Vehicle(vehicleType, vehicleRegistrationNo, vehicleLocation);
    }
}