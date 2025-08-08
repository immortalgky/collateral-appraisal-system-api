namespace Request.RequestComments.Features.RemoveRequestComment;

public record RemoveRequestCommentCommand(long CommentId) : ICommand<RemoveRequestCommentResult>;