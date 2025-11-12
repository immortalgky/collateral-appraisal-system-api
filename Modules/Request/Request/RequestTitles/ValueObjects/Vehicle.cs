namespace Request.RequestTitles.ValueObjects;

public class Vehicle : ValueObject
{
    public string? VehicleType { get; }
    public string? VehicleAppointmentLocation { get; }
    public string? ChassisNumber { get; }
    
    private Vehicle(
        string? vehicleType, 
        string? vehicleAppointmentLocation,
        string? chassisNumber
    )
    {
        VehicleType = vehicleType;
        VehicleAppointmentLocation = vehicleAppointmentLocation;
        ChassisNumber = chassisNumber;
    }


    public static Vehicle Create(
        string? vehicleType, 
        string? vehicleAppointmentLocation, 
        string? chassisNumber
    )
    {
        return new Vehicle(
            vehicleType, 
            vehicleAppointmentLocation, 
            chassisNumber
        );
    }
}