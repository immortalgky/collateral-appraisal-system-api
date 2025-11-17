using System;

namespace Request.Requests.ValueObjects;

public class Appointment : ValueObject
{
    public DateTime? AppointmentDateTime { get; }
    public string? AppointmentLocation { get; }

    private Appointment(DateTime? appointmentDate, string? locationDetail)
    {
        AppointmentDateTime = appointmentDate;
        AppointmentLocation = locationDetail;
    }

    private Appointment()
    {
        //EF Core
    }

    public static Appointment Create(DateTime? appointmentDate, string? locationDetail)
    {
        return new Appointment(appointmentDate, locationDetail);
    }
}
