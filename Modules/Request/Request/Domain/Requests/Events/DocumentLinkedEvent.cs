namespace Request.Domain.Requests.Events;

public record DocumentLinkedEvent(Guid RequestId, Guid DocumentId) : IDomainEvent;
