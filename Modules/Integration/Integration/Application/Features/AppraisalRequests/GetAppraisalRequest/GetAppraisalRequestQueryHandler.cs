using Request.Infrastructure.Repositories;
using Shared.CQRS;

namespace Integration.Application.Features.AppraisalRequests.GetAppraisalRequest;

public class GetAppraisalRequestQueryHandler(
    IRequestRepository requestRepository
) : IQueryHandler<GetAppraisalRequestQuery, GetAppraisalRequestResult>
{
    public async Task<GetAppraisalRequestResult> Handle(
        GetAppraisalRequestQuery query,
        CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(query.RequestId, cancellationToken);

        if (request is null)
        {
            throw new KeyNotFoundException($"Request {query.RequestId} not found");
        }

        return new GetAppraisalRequestResult(
            request.Id,
            request.RequestNumber?.Value,
            request.Status.Code,
            request.ExternalCaseKey,
            request.Purpose,
            request.Channel,
            request.Priority,
            request.RequestedAt,
            request.CreatedAt,
            request.CompletedAt
        );
    }
}
