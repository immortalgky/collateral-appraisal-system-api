namespace Request.RequestTitles.Events;

public record RequestTitleAddedEvent(Guid RequestId, RequestTitle RequestTitle) : IDomainEvent;