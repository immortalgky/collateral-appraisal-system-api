namespace Request.Application.Features.RequestComments.GetRequestCommentById;

public record GetRequestCommentByIdResult(
    Guid Id,
    Guid RequestId,
    string Comment,
    string CommentedBy,
    string CommentedByName,
    DateTime CommentedAt,
    DateTime? LastModifiedAt
);