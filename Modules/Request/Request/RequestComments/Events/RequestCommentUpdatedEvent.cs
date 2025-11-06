namespace Request.RequestComments.Events;

public record RequestCommentUpdatedEvent(Guid RequestId, RequestComment RequestComment, string PreviousComment) : IDomainEvent;