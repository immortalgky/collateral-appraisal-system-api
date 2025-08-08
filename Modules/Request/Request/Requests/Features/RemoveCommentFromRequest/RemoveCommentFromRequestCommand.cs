namespace Request.Requests.Features.RemoveCommentFromRequest;

public record RemoveCommentFromRequestCommand(long Id, long CommentId) : ICommand<RemoveCommentFromRequestResult>;