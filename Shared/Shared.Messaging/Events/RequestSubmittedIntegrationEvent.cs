using Request.Contracts.Requests.Dtos;

namespace Shared.Messaging.Events;

public record RequestSubmittedIntegrationEvent : IntegrationEvent
{
    public Guid RequestId { get; set; }
    public List<RequestTitleDto> RequestTitles { get; set; } = default!;
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
}