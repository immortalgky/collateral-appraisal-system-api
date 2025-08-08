namespace Request.RequestComments.Events;

public record RequestCommentAddedEvent(long RequestId, RequestComment RequestComment) : IDomainEvent;