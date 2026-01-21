using Request.Domain.Requests;
using Request.Infrastructure.Repositories;
using Shared.CQRS;

namespace Integration.Application.Features.AppraisalRequests.CancelAppraisalRequest;

public class CancelAppraisalRequestCommandHandler(
    IRequestRepository requestRepository
) : ICommandHandler<CancelAppraisalRequestCommand, CancelAppraisalRequestResult>
{
    public async Task<CancelAppraisalRequestResult> Handle(
        CancelAppraisalRequestCommand command,
        CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.RequestId, cancellationToken);

        if (request is null)
        {
            throw new KeyNotFoundException($"Request {command.RequestId} not found");
        }

        // Only allow cancellation of requests that are not already completed or cancelled
        if (request.Status == RequestStatus.Completed ||
            request.Status == RequestStatus.Cancelled)
        {
            throw new InvalidOperationException(
                $"Cannot cancel request in status {request.Status.Code}");
        }

        request.UpdateStatus(RequestStatus.Cancelled);
        await requestRepository.SaveChangesAsync(cancellationToken);

        return new CancelAppraisalRequestResult(true);
    }
}
