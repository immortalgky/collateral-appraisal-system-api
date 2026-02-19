namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Appointment entity for property survey visits.
/// Linked to an assignment (not appraisal directly).
/// </summary>
public class Appointment : Entity<Guid>
{
    private readonly List<AppointmentHistory> _history = [];
    public IReadOnlyList<AppointmentHistory> History => _history.AsReadOnly();

    public Guid AssignmentId { get; private set; }

    // Appointment Details
    public DateTime AppointmentDateTime { get; private set; }
    public DateTime? ProposedDate { get; private set; }
    public string? LocationDetail { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    // Status
    public string Status { get; private set; } = null!; // Pending, Approved, Completed, Cancelled
    public DateTime? ActionDate { get; private set; }
    public string? Reason { get; private set; }

    // Approval (reschedule only)
    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public int RescheduleCount { get; private set; }

    // Contact Person
    public string AppointedBy { get; private set; } = default!;
    public string? ContactPerson { get; private set; }
    public string? ContactPhone { get; private set; }

    private Appointment()
    {
        // For EF Core
    }

    public static Appointment Create(
        Guid assignmentId,
        DateTime appointmentDateTime,
        string appointedBy,
        string? locationDetail = null,
        string? contactPerson = null,
        string? contactPhone = null)
    {
        return new Appointment
        {
            Id = Guid.CreateVersion7(),
            AssignmentId = assignmentId,
            AppointmentDateTime = appointmentDateTime,
            AppointedBy = appointedBy,
            LocationDetail = locationDetail,
            ContactPerson = contactPerson,
            ContactPhone = contactPhone,
            Status = "Pending",
            RescheduleCount = 0
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

    public void Approve(string approvedBy)
    {
        if (Status != "Pending")
            throw new InvalidOperationException($"Cannot approve appointment in status '{Status}'");

        RecordHistory("StatusChanged", approvedBy, "Approved from Pending");
        Status = "Approved";
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
        ActionDate = DateTime.UtcNow;
    }

    public void Complete(string changedBy)
    {
        if (Status != "Approved")
            throw new InvalidOperationException($"Cannot complete appointment in status '{Status}'");

        RecordHistory("StatusChanged", changedBy, "Completed");
        Status = "Completed";
        ActionDate = DateTime.UtcNow;
    }

    public void Cancel(string changedBy, string? reason = null)
    {
        if (Status == "Completed")
            throw new InvalidOperationException("Cannot cancel a completed appointment");

        RecordHistory("Cancelled", changedBy, reason);
        Status = "Cancelled";
        Reason = reason;
        ActionDate = DateTime.UtcNow;
    }

    public void Reschedule(string changedBy, DateTime newDate, string? reason = null)
    {
        if (Status == "Completed" || Status == "Cancelled")
            throw new InvalidOperationException($"Cannot reschedule appointment in status '{Status}'");

        RecordHistory("Rescheduled", changedBy, reason);
        AppointmentDateTime = newDate;
        Reason = reason;
        RescheduleCount++;
        Status = "Pending"; // Reset to pending for re-approval
        ActionDate = DateTime.UtcNow;
    }

    private void RecordHistory(string changeType, string changedBy, string? reason)
    {
        var history = AppointmentHistory.Create(
            Id, AppointmentDateTime, Status, LocationDetail, changeType, reason, changedBy);
        _history.Add(history);
    }
}