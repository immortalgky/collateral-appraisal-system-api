namespace Shared.Messaging.Events;

public record AppraisalCreatedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid RequestId { get; init; }
    public string? AppraisalNumber { get; init; }
    public string? AppraisalType { get; init; }
    public string? CreatedBy { get; init; }
    public string? RequestedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsPma { get; init; }
    public decimal? FacilityLimit { get; init; }
    public string? Priority { get; init; }
    public bool HasAppraisalBook { get; init; }
    public string? Channel { get; init; }

    /// <summary>
    /// Appointment date from the initial creation request, when one was supplied.
    /// Carried here so the Workflow consumer can write <c>appointmentDate</c> into
    /// <c>WorkflowInstance.Variables</c> in the same transaction as the appraisal-created
    /// signal — avoiding a second concurrent writer that would race on the RowVersion token.
    /// </summary>
    public DateTime? AppointmentDateTime { get; init; }
}