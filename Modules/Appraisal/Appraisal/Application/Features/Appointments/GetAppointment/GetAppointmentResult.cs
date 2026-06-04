namespace Appraisal.Application.Features.Appointments.GetAppointment;

public record GetAppointmentResult(AppointmentDto? Appointment);

public sealed record AppointmentDto
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid AppraisalId { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public DateTime? ProposedDate { get; set; }
    public string? LocationDetail { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string Status { get; set; } = default!;
    public string? Reason { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int RescheduleCount { get; set; }
    public string AppointedBy { get; set; } = default!;
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime? CreatedOn { get; set; }

    // Inline-edit approval markers (added for the inline-edit + pending-approval feature)
    public bool RequiresApproval { get; set; }
    public DateTime? ApprovalSubmittedAt { get; set; }

    // The previous (pre-reschedule) date, shown struck-through next to the proposed date while
    // the reschedule is awaiting approval. Null unless RequiresApproval.
    public DateTime? PreviousDate { get; set; }
}
