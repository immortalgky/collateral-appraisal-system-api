namespace Request.RequestComments.Features.RemoveRequestComment;

public record RemoveRequestCommentCommand(Guid CommentId) : ICommand<RemoveRequestCommentResult>;