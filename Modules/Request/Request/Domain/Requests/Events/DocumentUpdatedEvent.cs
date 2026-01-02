namespace Request.Domain.Requests.Events;

public record DocumentUpdatedEvent(Guid RequestId, Guid PreviousDocumentId, Guid DocumentId) : IDomainEvent;
