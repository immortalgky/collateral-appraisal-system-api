namespace Request.RequestComments.Events;

public record RequestCommentAddedEvent(Guid RequestId, RequestComment RequestComment) : IDomainEvent;