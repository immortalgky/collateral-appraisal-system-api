namespace Appraisal.Application.Features.Appointments.GetAppointmentHistory;

public record GetAppointmentHistoryResult(IReadOnlyList<AppointmentHistoryEventDto> Events);

public sealed record AppointmentHistoryEventDto
{
    /// <summary>
    /// One of: AppointmentRescheduled, AppointmentApproved, AppointmentRejected,
    /// AppointmentCancelled, FeeAdded, FeeApproved, FeeRejected.
    /// </summary>
    public string EventType { get; set; } = default!;

    public string Title { get; set; } = default!;

    public DateTime? OldDate { get; set; }
    public DateTime? NewDate { get; set; }

    public string? FeeCode { get; set; }
    public string? FeeDescription { get; set; }
    public decimal? Amount { get; set; }

    /// <summary>Approved | Rejected | Pending | Auto</summary>
    public string? Status { get; set; }

    public string? ActorCode { get; set; }
    public string? ActorName { get; set; }
    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; }
}
