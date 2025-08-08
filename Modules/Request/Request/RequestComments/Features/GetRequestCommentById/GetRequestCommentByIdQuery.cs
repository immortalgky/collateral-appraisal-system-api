namespace Request.RequestComments.Features.GetRequestCommentById;

public record GetRequestCommentByIdQuery(long RequestId, long CommentId) : IQuery<GetRequestCommentByIdResult>;