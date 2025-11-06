namespace Request.RequestComments.Features.GetRequestCommentsByRequestId;

public record GetRequestCommentsByRequestIdResult(List<RequestCommentDto> Comments);

public record RequestCommentDto(
    Guid Id,
    Guid RequestId,
    string Comment,
    string CommentedBy,
    string CommentedByName,
    DateTime CommentedAt
);