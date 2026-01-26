namespace Request.Application.Services;

public interface ICreateRequestService
{
    Task<Request.Domain.Requests.Request> CreateRequestAsync(CreateRequestData data,
        CancellationToken cancellationToken);
}