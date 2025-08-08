namespace Request.RequestComments.Features.GetRequestCommentsByRequestId;

public record GetRequestCommentsByRequestIdResult(List<RequestCommentDto> Comments);

public record RequestCommentDto(
    long Id,
    long RequestId,
    string Comment,
    DateTime? CreatedOn,
    string? CreatedBy,
    DateTime? UpdatedOn,
    string? UpdatedBy
);