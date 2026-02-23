namespace Request.Application.Services;

public interface IUpdateRequestService
{
Task<Request.Domain.Requests.Request> ResubmitRequestAsync(ResubmitRequestData command,
        CancellationToken cancellationToken);
}
