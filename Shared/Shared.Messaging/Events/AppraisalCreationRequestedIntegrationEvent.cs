using Request.Contracts.Requests.Dtos;

namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Workflow module when an appraisal should be created for a request.
/// Consumed by the Appraisal module to trigger appraisal creation.
/// The timing is table-driven: immediate for non-manual channels, deferred for manual.
/// </summary>
public record AppraisalCreationRequestedIntegrationEvent : IntegrationEvent
{
    public Guid RequestId { get; set; }
    public List<RequestTitleDto> RequestTitles { get; set; } = default!;
    public AppointmentDto? Appointment { get; set; }
    public FeeDto? Fee { get; set; }
    public ContactDto? Contact { get; set; }
    public string? CreatedBy { get; set; }
    public string? Priority { get; set; }
    public bool IsPma { get; set; }
    public string? Purpose { get; set; }
    public string? Channel { get; set; }
    public string? BankingSegment { get; set; }
    public decimal? FacilityLimit { get; set; }
    public bool HasAppraisalBook { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime? RequestedAt { get; set; }

    // Construction Inspection fields
    public Guid? PrevAppraisalId { get; set; }
    public string? AppraisalType { get; set; }

    // Reappraisal batch label — NULL for non-reappraisal requests.
    // Stamps Appraisal.GroupTag on the newly-created appraisal.
    public string? GroupTag { get; set; }

    // Workflow definition the appraisal is created under. Carried so the Appraisal module can
    // resolve the workflow-scope SLA budget (SlaPolicy) and stamp the appraisal-level SLA fields.
    public Guid? WorkflowDefinitionId { get; set; }
}
