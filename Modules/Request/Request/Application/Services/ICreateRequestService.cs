namespace Request.Application.Services;

public interface ICreateRequestService
{
    Task<(Request.Domain.Requests.Request,List<RequestTitle>)> CreateRequestAsync(CreateRequestData data,
        CancellationToken cancellationToken);
}