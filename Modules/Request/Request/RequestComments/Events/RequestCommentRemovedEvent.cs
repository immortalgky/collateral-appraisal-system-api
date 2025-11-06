namespace Request.RequestComments.Events;

public record RequestCommentRemovedEvent(Guid RequestId, Guid CommentId, string Comment, string? RemovedBy) : IDomainEvent;