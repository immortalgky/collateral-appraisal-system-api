namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Historical records of appointment changes (reschedules, cancellations).
/// </summary>
public class AppointmentHistory : Entity<Guid>
{
    public Guid AppointmentId { get; private set; }

    // Original Values (snapshot at time of change)
    public DateTime PreviousAppointmentDateTime { get; private set; }
    public string PreviousStatus { get; private set; } = null!;
    public string? PreviousLocationDetail { get; private set; }

    // Change Details
    public string ChangeType { get; private set; } = null!; // Rescheduled, Cancelled, StatusChanged
    public string? ChangeReason { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public string ChangedBy { get; private set; } = default!;

    private AppointmentHistory()
    {
        // For EF Core
    }

    public static AppointmentHistory Create(
        Guid appointmentId,
        DateTime previousAppointmentDateTime,
        string previousStatus,
        string? previousLocationDetail,
        string changeType,
        string? changeReason,
        string changedBy)
    {
        if (changeType != "Rescheduled" && changeType != "Cancelled" && changeType != "StatusChanged")
            throw new ArgumentException("ChangeType must be 'Rescheduled', 'Cancelled', or 'StatusChanged'");

        return new AppointmentHistory
        {
            //Id = Guid.CreateVersion7(),
            AppointmentId = appointmentId,
            PreviousAppointmentDateTime = previousAppointmentDateTime,
            PreviousStatus = previousStatus,
            PreviousLocationDetail = previousLocationDetail,
            ChangeType = changeType,
            ChangeReason = changeReason,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = changedBy
        };
    }
}