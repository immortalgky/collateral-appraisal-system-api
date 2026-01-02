namespace Request.Domain.RequestComments.Events;

public record RequestCommentUpdatedEvent(Guid RequestId, RequestComment RequestComment, string PreviousComment) : IDomainEvent;
