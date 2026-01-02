namespace Request.Contracts.Requests.Dtos;

public record RequestCommentDto(
    Guid Id,
    Guid RequestId,
    string Comment,
    string CommentedBy,
    string CommentedByName,
    DateTime CommentedAt,
    DateTime? LastModifiedAt
);