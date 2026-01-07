namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Historical records of appointment changes (reschedules, cancellations).
/// </summary>
public class AppointmentHistory : Entity<Guid>
{
    public Guid AppointmentId { get; private set; }
    public Guid AppraisalId { get; private set; }

    // Original Values (snapshot at time of change)
    public DateTime PreviousAppointmentDate { get; private set; }
    public string PreviousStatus { get; private set; } = null!;
    public string? PreviousLocationDetail { get; private set; }

    // Change Details
    public string ChangeType { get; private set; } = null!; // Rescheduled, Cancelled, StatusChanged
    public string? ChangeReason { get; private set; }
    public DateTime ChangedOn { get; private set; }
    public Guid ChangedBy { get; private set; }

    private AppointmentHistory()
    {
    }

    public static AppointmentHistory Create(
        Guid appointmentId,
        Guid appraisalId,
        DateTime previousAppointmentDate,
        string previousStatus,
        string? previousLocationDetail,
        string changeType,
        string? changeReason,
        Guid changedBy)
    {
        if (changeType != "Rescheduled" && changeType != "Cancelled" && changeType != "StatusChanged")
            throw new ArgumentException("ChangeType must be 'Rescheduled', 'Cancelled', or 'StatusChanged'");

        return new AppointmentHistory
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointmentId,
            AppraisalId = appraisalId,
            PreviousAppointmentDate = previousAppointmentDate,
            PreviousStatus = previousStatus,
            PreviousLocationDetail = previousLocationDetail,
            ChangeType = changeType,
            ChangeReason = changeReason,
            ChangedOn = DateTime.UtcNow,
            ChangedBy = changedBy
        };
    }
}