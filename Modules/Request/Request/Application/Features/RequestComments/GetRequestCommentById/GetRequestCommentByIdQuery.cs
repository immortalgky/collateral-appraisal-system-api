namespace Request.Application.Features.RequestComments.GetRequestCommentById;

public record GetRequestCommentByIdQuery(Guid RequestId, Guid CommentId) : IQuery<GetRequestCommentByIdResult>;
