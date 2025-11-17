namespace Request.RequestComments.Events;

public record RequestCommentRemovedEvent(Guid RequestId, long CommentId, string Comment, string? RemovedBy)
    : IDomainEvent;