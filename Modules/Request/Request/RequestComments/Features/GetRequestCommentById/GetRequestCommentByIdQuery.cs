namespace Request.RequestComments.Features.GetRequestCommentById;

public record GetRequestCommentByIdQuery(Guid RequestId, long CommentId) : IQuery<GetRequestCommentByIdResult>;