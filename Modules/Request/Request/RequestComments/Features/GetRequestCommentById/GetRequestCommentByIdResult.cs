namespace Request.RequestComments.Features.GetRequestCommentById;

public record GetRequestCommentByIdResult(
    Guid Id,
    Guid RequestId,
    string Comment,
    DateTime? CreatedOn,
    string? CreatedBy,
    DateTime? UpdatedOn,
    string? UpdatedBy
);