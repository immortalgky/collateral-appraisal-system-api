namespace Request.Domain.Requests.Events;

public record DocumentUnlinkedEvent(Guid RequestId, Guid DocumentId) : IDomainEvent;
