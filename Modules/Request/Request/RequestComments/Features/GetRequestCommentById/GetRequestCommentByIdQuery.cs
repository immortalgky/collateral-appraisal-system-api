namespace Request.RequestComments.Features.GetRequestCommentById;

public record GetRequestCommentByIdQuery(Guid RequestId, Guid CommentId) : IQuery<GetRequestCommentByIdResult>;
