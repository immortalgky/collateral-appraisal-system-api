namespace Request.Domain.RequestComments.Events;

public record RequestCommentAddedEvent(Guid RequestId, RequestComment RequestComment) : IDomainEvent;