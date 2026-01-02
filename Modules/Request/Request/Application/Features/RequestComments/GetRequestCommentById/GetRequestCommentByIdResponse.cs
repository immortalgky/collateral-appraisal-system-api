namespace Request.Application.Features.RequestComments.GetRequestCommentById;

public record GetRequestCommentByIdResponse(
    Guid Id,
    Guid RequestId,
    string Comment,
    string CommentedBy,
    string CommentedByName,
    DateTime CommentedAt,
    DateTime? LastModifiedAt
);