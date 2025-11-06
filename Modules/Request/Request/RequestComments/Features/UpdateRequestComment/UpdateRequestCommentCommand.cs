namespace Request.RequestComments.Features.UpdateRequestComment;

public record UpdateRequestCommentCommand(Guid CommentId, string Comment) : ICommand<UpdateRequestCommentResult>;