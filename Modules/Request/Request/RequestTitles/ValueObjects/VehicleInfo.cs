namespace Request.RequestTitles.ValueObjects;

public class VehicleInfo : ValueObject
{
    public string? VehicleType { get; }
    public string? VehicleAppointmentLocation { get; }
    public string? ChassisNumber { get; }
    
    private VehicleInfo(
        string? vehicleType, 
        string? vehicleAppointmentLocation,
        string? chassisNumber
    )
    {
        VehicleType = vehicleType;
        VehicleAppointmentLocation = vehicleAppointmentLocation;
        ChassisNumber = chassisNumber;
    }


    public static VehicleInfo Create(
        string? vehicleType, 
        string? vehicleAppointmentLocation, 
        string? chassisNumber
    )
    {
        return new VehicleInfo(
            vehicleType, 
            vehicleAppointmentLocation, 
            chassisNumber
        );
    }
}