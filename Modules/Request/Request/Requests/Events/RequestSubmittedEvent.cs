namespace Request.Requests.Events;

public record RequestSubmittedEvent(Models.Request Request) : IDomainEvent;
