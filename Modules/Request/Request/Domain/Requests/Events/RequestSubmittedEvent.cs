namespace Request.Domain.Requests.Events;

public record RequestSubmittedEvent(Domain.Requests.Request Request) : IDomainEvent;
