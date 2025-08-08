namespace Request.Requests.ValueObjects;

public class Vehicle : ValueObject
{
    private Vehicle(string? vehicleType, string? vehicleRegistrationNo, string? vehAppointmentLocation)
    {
        VehicleType = vehicleType;
        VehicleRegistrationNo = vehicleRegistrationNo;
        VehAppointmentLocation = vehAppointmentLocation;
    }
    public string? VehicleType { get; } = default!;
    public string? VehicleRegistrationNo { get; } = default!;
    public string? VehAppointmentLocation { get; } = default!;

    public static Vehicle Create(string? vehicleType, string? vehicleRegistrationNo, string? vehAppointmentLocation)
    {
        return new Vehicle(vehicleType, vehicleRegistrationNo, vehAppointmentLocation);
    }

}