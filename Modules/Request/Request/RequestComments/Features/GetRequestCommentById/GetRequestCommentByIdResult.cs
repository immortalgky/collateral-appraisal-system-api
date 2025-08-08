namespace Request.RequestComments.Features.GetRequestCommentById;

public record GetRequestCommentByIdResult(
    long Id,
    long RequestId,
    string Comment,
    DateTime? CreatedOn,
    string? CreatedBy,
    DateTime? UpdatedOn,
    string? UpdatedBy
);