using Request.Contracts.Requests.Dtos;

namespace Shared.Messaging.Events;

public record RequestSubmittedIntegrationEvent : IntegrationEvent
{
    public Guid RequestId { get; set; }
    public List<RequestTitleDto> RequestTitles { get; set; } = default!;
}