namespace Request.RequestComments.Features.UpdateRequestComment;

public record UpdateRequestCommentCommand(long CommentId, string Comment) : ICommand<UpdateRequestCommentResult>;