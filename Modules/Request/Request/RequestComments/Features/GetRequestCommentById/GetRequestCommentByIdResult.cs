namespace Request.RequestComments.Features.GetRequestCommentById;

public record GetRequestCommentByIdResult(
    long Id,
    Guid RequestId,
    string Comment,
    DateTime? CreatedOn,
    string? CreatedBy,
    DateTime? UpdatedOn,
    string? UpdatedBy
);