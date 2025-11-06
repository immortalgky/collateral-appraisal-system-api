namespace Request.RequestTitles.ValueObjects;

public class Vehicle : ValueObject
{
    public string? VehicleType { get; }
    public string? VehicleRegistrationNo { get; }
    public string? VehicleAppointmentLocation { get; }
    public string? ChassisNumber { get; } // use for key
    
    private Vehicle(string? vehicleType, string? vehicleRegistrationNo, string? vehicleLocation)
    {
        VehicleType = vehicleType;
        VehicleRegistrationNo = vehicleRegistrationNo;
        VehicleLocation = vehicleLocation;
    }


    public static Vehicle Create(string? vehicleType, string? vehicleRegistrationNo, string? vehicleLocation)
    {
        return new Vehicle(vehicleType, vehicleRegistrationNo, vehicleLocation);
    }
}