namespace Request.RequestComments.Features.GetRequestCommentById;

public record GetRequestCommentByIdResponse(
    Guid Id,
    Guid RequestId,
    string Comment,
    DateTime? CreatedOn,
    string? CreatedBy,
    DateTime? UpdatedOn,
    string? UpdatedBy
);