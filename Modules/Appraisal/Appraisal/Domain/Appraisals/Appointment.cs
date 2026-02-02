namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Appointment entity for property survey visits.
/// </summary>
public class Appointment : Entity<Guid>
{
    private readonly List<AppointmentHistory> _history = [];
    public IReadOnlyList<AppointmentHistory> History => _history.AsReadOnly();

    public Guid AppraisalId { get; private set; }

    // Appointment Details
    public DateTime AppointmentDate { get; private set; }
    public string? LocationDetail { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    // Status
    public string Status { get; private set; } = null!; // Pending, Approved, Completed, Cancelled
    public DateTime? ActionDate { get; private set; }

    // Contact Person
    public Guid AppointedBy { get; private set; }
    public string? ContactPerson { get; private set; }
    public string? ContactPhone { get; private set; }

    private Appointment()
    {
    }

    public static Appointment Create(
        Guid appraisalId,
        DateTime appointmentDate,
        Guid appointedBy,
        string? locationDetail = null,
        string? contactPerson = null,
        string? contactPhone = null)
    {
        return new Appointment
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            AppointmentDate = appointmentDate,
            AppointedBy = appointedBy,
            LocationDetail = locationDetail,
            ContactPerson = contactPerson,
            ContactPhone = contactPhone,
            Status = "Pending"
        };
    }

    public void SetLocation(decimal latitude, decimal longitude, string? detail = null)
    {
        Latitude = latitude;
        Longitude = longitude;
        LocationDetail = detail ?? LocationDetail;
    }

    public void SetContactInfo(string? person, string? phone)
    {
        ContactPerson = person;
        ContactPhone = phone;
    }

    public void Approve(Guid changedBy)
    {
        if (Status != "Pending")
            throw new InvalidOperationException($"Cannot approve appointment in status '{Status}'");

        RecordHistory("StatusChanged", changedBy, "Approved from Pending");
        Status = "Approved";
        ActionDate = DateTime.UtcNow;
    }

    public void Complete(Guid changedBy)
    {
        if (Status != "Approved")
            throw new InvalidOperationException($"Cannot complete appointment in status '{Status}'");

        RecordHistory("StatusChanged", changedBy, "Completed");
        Status = "Completed";
        ActionDate = DateTime.UtcNow;
    }

    public void Cancel(Guid changedBy, string? reason = null)
    {
        if (Status == "Completed")
            throw new InvalidOperationException("Cannot cancel a completed appointment");

        RecordHistory("Cancelled", changedBy, reason);
        Status = "Cancelled";
        ActionDate = DateTime.UtcNow;
    }

    public void Reschedule(Guid changedBy, DateTime newDate, string? reason = null)
    {
        if (Status == "Completed" || Status == "Cancelled")
            throw new InvalidOperationException($"Cannot reschedule appointment in status '{Status}'");

        RecordHistory("Rescheduled", changedBy, reason);
        AppointmentDate = newDate;
        ActionDate = DateTime.UtcNow;
    }

    private void RecordHistory(string changeType, Guid changedBy, string? reason)
    {
        var history = AppointmentHistory.Create(
            Id, AppraisalId, AppointmentDate, Status, LocationDetail, changeType, reason, changedBy);
        _history.Add(history);
    }
}