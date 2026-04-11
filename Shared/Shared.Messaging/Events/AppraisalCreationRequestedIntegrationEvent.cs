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
}
