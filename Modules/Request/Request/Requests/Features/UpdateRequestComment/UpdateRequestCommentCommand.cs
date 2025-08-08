namespace Request.Requests.Features.UpdateRequestComment;

public record UpdateRequestCommentCommand(long Id, long CommentId, string Comment)
    : ICommand<UpdateRequestCommentResult>;