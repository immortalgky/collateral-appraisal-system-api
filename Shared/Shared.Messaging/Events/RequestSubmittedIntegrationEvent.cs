using Request.Contracts.Requests.Dtos;

namespace Shared.Messaging.Events;

public record RequestSubmittedIntegrationEvent : IntegrationEvent
{
    public Guid RequestId { get; set; }
    public List<RequestTitleDto> RequestTitles { get; set; } = default!;
    public List<RequestPropertyDto> RequestProperties { get; set; } = [];
    public AppointmentDto? Appointment { get; set; }
    public FeeDto? Fee { get; set; }
    public ContactDto? Contact { get; set; }
    public string? CreatedBy { get; set; }

    // Request-level properties needed for appraisal creation and workflow routing
    public string? Priority { get; set; }
    public bool IsPma { get; set; }
    public string? Purpose { get; set; }
    public string? Channel { get; set; }
    public string? BankingSegment { get; set; }
    public decimal? FacilityLimit { get; set; }
    public bool HasAppraisalBook { get; set; }

    // Request metadata denormalized onto Appraisal
    public string? RequestedBy { get; set; }
    public DateTime? RequestedAt { get; set; }

    // Construction Inspection fields
    public Guid? PrevAppraisalId { get; set; }

    // The prior book's number when it is NOT an appraisal in this system (legacy AS400 "99A…").
    // Never set together with PrevAppraisalId.
    public string? PrevAppraisalNumber { get; set; }
    public string? AppraisalType { get; set; }

    // Reappraisal batch label — NULL for non-reappraisal requests.
    // Flows through to Appraisal.GroupTag; persisted on Request (Request.GroupTag) for AS400 reappraisal.
    public string? GroupTag { get; set; }

    // How the request entered the system — "UI" vs "API". Drives whether the workflow
    // applies the appraisal-initiation-check task. Distinct from business Channel; not stored
    // on Request. "SIBS" for an AS400 periodical reappraisal (Request.GroupTag set).
    public string? EntrySource { get; set; }
}