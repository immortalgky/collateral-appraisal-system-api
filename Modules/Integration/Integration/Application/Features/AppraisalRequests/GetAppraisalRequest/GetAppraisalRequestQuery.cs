using Shared.CQRS;

namespace Integration.Application.Features.AppraisalRequests.GetAppraisalRequest;

public record GetAppraisalRequestQuery(Guid RequestId) : IQuery<GetAppraisalRequestResult>;

public record GetAppraisalRequestResult(
    Guid Id,
    string? RequestNumber,
    string Status,
    string? ExternalCaseKey,
    string? Purpose,
    string? Channel,
    string? Priority,
    DateTime? RequestedAt,
    DateTime CreatedAt,
    DateTime? CompletedAt
);
