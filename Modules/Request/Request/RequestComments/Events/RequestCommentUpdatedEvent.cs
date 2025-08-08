namespace Request.RequestComments.Events;

public record RequestCommentUpdatedEvent(long RequestId, RequestComment RequestComment, string PreviousComment) : IDomainEvent;