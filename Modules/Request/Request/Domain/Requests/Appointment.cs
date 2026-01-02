namespace Request.Domain.Requests;

public class Appointment : ValueObject
{
    public DateTime? AppointmentDate { get; }
    public string? AppointmentLocation { get; }

    private Appointment(DateTime? appointmentDate, string? appointmentLocation)
    {
        AppointmentDate = appointmentDate;
        AppointmentLocation = appointmentLocation;
    }

    public static Appointment Create(DateTime? appointmentDate, string? appointmentLocation)
    {
        return new Appointment(appointmentDate, appointmentLocation);
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(AppointmentDate);
        ArgumentException.ThrowIfNullOrWhiteSpace(AppointmentLocation);
    }
}