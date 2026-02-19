namespace Request.Domain.Requests.Events;

public record RequestSubmittedEvent(Request Request) : IDomainEvent;