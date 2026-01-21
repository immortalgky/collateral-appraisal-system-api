using Shared.CQRS;

namespace Integration.Application.Features.AppraisalRequests.CancelAppraisalRequest;

public record CancelAppraisalRequestCommand(
    Guid RequestId,
    string? Reason
) : ICommand<CancelAppraisalRequestResult>;

public record CancelAppraisalRequestResult(bool Success);
