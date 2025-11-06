namespace Request.Requests.ValueObjects;

public class Appointment : ValueObject
{
    public DateTime? AppointmentDateTime { get; private set; }
    public string? AppointmentLocation { get; private set; }

    public static Appointment Create(DateTime? appointmentDateTime, string? appointmentLocation)
    {
        // validate appointmentLocation not null
        
        return new Appointment()
        {
            AppointmentDateTime = appointmentDateTime,
            AppointmentLocation = appointmentLocation
        };
    }
}
