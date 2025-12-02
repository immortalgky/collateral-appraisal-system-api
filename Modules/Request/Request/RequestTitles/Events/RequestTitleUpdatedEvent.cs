namespace Request.RequestTitles.Events;

public record RequestTitleUpdatedEvent(Guid RequestId, RequestTitle RequestTitle) : IDomainEvent;