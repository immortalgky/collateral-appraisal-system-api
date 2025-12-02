namespace Request.RequestTitles.Events;

public record RequestTitleCreatedEvent(Guid RequestId, RequestTitle RequestTitle) : IDomainEvent;