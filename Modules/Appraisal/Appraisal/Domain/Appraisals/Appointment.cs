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
    public string Status { get; private set; } = null!; // Appointed, Pending, Cancelled
    public DateTime? ActionDate { get; private set; }
    public string? Reason { get; private set; }

    // Approval (reschedule only)
    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public int RescheduleCount { get; private set; }

    // Inline-edit approval markers
    /// <summary>
    /// True when the appointment is in Pending state and an approval will be needed
    /// (set by the inline reschedule/create handlers after policy evaluation).
    /// Cleared when auto-approved or when the approval is resolved.
    /// </summary>
    public bool RequiresApproval { get; private set; }

    /// <summary>
    /// Stamped when Submit for Approval is called. Null means the change is a draft
    /// that has not yet been submitted for bank review.
    /// </summary>
    public DateTime? ApprovalSubmittedAt { get; private set; }

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
            Status = "Appointed",
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

        RecordHistory("StatusChanged", approvedBy, "Appointed from Pending");
        Status = "Appointed";
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.Now;
        ActionDate = DateTime.Now;
        // Clear approval markers — auto-applied or bank-approved; no pending draft remains.
        RequiresApproval = false;
        ApprovalSubmittedAt = null;
    }

    public void Cancel(string changedBy, string? reason = null)
    {
        if (Status == "Cancelled")
            throw new InvalidOperationException("Cannot cancel an already-cancelled appointment");

        RecordHistory("Cancelled", changedBy, reason);
        Status = "Cancelled";
        Reason = reason;
        ActionDate = DateTime.Now;
    }

    public void Reschedule(string changedBy, DateTime newDate, string? reason = null)
    {
        if (Status == "Cancelled")
            throw new InvalidOperationException($"Cannot reschedule appointment in status '{Status}'");

        RecordHistory("Rescheduled", changedBy, reason);
        AppointmentDateTime = newDate;
        Reason = reason;
        RescheduleCount++;
        Status = "Pending"; // Reset to pending for re-approval
        ActionDate = DateTime.Now;
    }

    /// <summary>
    /// Flags this appointment as requiring bank approval and moves it to Pending status
    /// (set after policy evaluation in the reschedule handler when the policy returns true).
    /// Status and RequiresApproval move in lockstep.
    /// </summary>
    public void FlagRequiresApproval()
    {
        Status = "Pending";
        RequiresApproval = true;
    }

    /// <summary>
    /// Stamps the moment the pending appointment was submitted for bank approval.
    /// Prevents further edits until the approval is resolved.
    /// </summary>
    public void MarkApprovalSubmitted()
    {
        ApprovalSubmittedAt = DateTime.Now;
    }

    /// <summary>
    /// Clears the approval markers after a resolution (approve or reject) so that
    /// a fresh draft can begin after a rejection.
    /// </summary>
    public void ClearApprovalMarkers()
    {
        RequiresApproval = false;
        ApprovalSubmittedAt = null;
    }

    /// <summary>
    /// Reverts the appointment to the last confirmed date when a reschedule request is rejected.
    /// Decrements RescheduleCount (undo the increment from Reschedule).
    /// Sets Status back to Appointed — the previously-confirmed date is reinstated automatically.
    /// </summary>
    public void RejectReschedule(string changedBy, string? reason = null)
    {
        // Find the date that was active just before the most recent reschedule.
        // The most recent "Rescheduled" history entry captured the prior date.
        var lastRescheduledHistory = _history
            .Where(h => h.ChangeType == "Rescheduled")
            .OrderByDescending(h => h.ChangedAt)
            .FirstOrDefault();

        RecordHistory("RescheduleRejected", changedBy, reason); // distinct from a user cancellation — this is an approver rejecting a pending reschedule

        if (lastRescheduledHistory is not null)
        {
            AppointmentDateTime = lastRescheduledHistory.PreviousAppointmentDateTime;
            if (RescheduleCount > 0) RescheduleCount--;
            Status = "Appointed"; // Restore to Appointed — the previously-confirmed date is reinstated
        }

        Reason = reason;
        ActionDate = DateTime.Now;
    }

    /// <summary>
    /// Discards a draft reschedule that is in Pending status but has NOT yet been submitted
    /// for approval (ApprovalSubmittedAt is null). Reverts AppointmentDateTime to the last
    /// confirmed date, decrements RescheduleCount, and returns Status to "Appointed".
    /// </summary>
    public void CancelReschedule(string changedBy, string? reason = null)
    {
        if (Status != "Pending")
            throw new InvalidOperationException($"Cannot cancel reschedule in status '{Status}'");

        RecordHistory("RescheduleCancelled", changedBy, reason);

        var lastRescheduledHistory = _history
            .Where(h => h.ChangeType == "Rescheduled")
            .OrderByDescending(h => h.ChangedAt)
            .FirstOrDefault();

        if (lastRescheduledHistory is not null)
        {
            AppointmentDateTime = lastRescheduledHistory.PreviousAppointmentDateTime;
            if (RescheduleCount > 0) RescheduleCount--;
            Status = "Appointed";
        }

        Reason = reason;
        ActionDate = DateTime.Now;
        ClearApprovalMarkers();
    }

    private void RecordHistory(string changeType, string changedBy, string? reason)
    {
        var history = AppointmentHistory.Create(
            Id, AppointmentDateTime, Status, LocationDetail, changeType, reason, changedBy);
        _history.Add(history);
    }
}