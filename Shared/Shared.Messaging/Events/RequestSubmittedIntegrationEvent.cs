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
}