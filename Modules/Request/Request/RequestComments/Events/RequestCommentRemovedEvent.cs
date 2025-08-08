namespace Request.RequestComments.Events;

public record RequestCommentRemovedEvent(long RequestId, long CommentId, string Comment, string? RemovedBy) : IDomainEvent;