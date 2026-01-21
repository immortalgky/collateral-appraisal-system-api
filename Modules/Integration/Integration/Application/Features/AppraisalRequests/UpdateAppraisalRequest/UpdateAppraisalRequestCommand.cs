using Integration.Application.Features.AppraisalRequests.CreateAppraisalRequest;
using Shared.CQRS;

namespace Integration.Application.Features.AppraisalRequests.UpdateAppraisalRequest;

public record UpdateAppraisalRequestCommand(
    Guid RequestId,
    string? Priority,
    AppraisalRequestContact? Contact,
    List<AppraisalRequestCustomer>? Customers,
    List<AppraisalRequestProperty>? Properties
) : ICommand<UpdateAppraisalRequestResult>;

public record UpdateAppraisalRequestResult(bool Success);
