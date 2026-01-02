namespace Request.Domain.Requests.Events;

public record RequestCreatedEvent(Domain.Requests.Request Request) : IDomainEvent;