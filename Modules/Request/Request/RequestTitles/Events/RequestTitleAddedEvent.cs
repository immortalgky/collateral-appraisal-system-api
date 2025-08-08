namespace Request.RequestTitles.Events;

public record RequestTitleAddedEvent(long RequestId, RequestTitle RequestTitle) : IDomainEvent;