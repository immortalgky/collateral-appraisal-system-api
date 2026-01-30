namespace Request.Domain.Requests;

public class Appointment : ValueObject
{
    public DateTime? AppointmentDateTime { get; }
    public string? AppointmentLocation { get; }

    private Appointment(DateTime? appointmentDateTime, string? appointmentLocation)
    {
        AppointmentDateTime = appointmentDateTime;
        AppointmentLocation = appointmentLocation;
    }

    public static Appointment Create(DateTime? appointmentDateTime, string? appointmentLocation)
    {
        return new Appointment(appointmentDateTime, appointmentLocation);
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(AppointmentDateTime);
        ArgumentException.ThrowIfNullOrWhiteSpace(AppointmentLocation);
    }
}