using Request.Requests.Features.CreateDraftRequest;
using Request.Requests.Features.CreateRequest;
using Request.Requests.Features.UpdateDraftRequest;
using Request.Requests.Features.UpdateRequest;

namespace Request.Services;

public interface IRequestService
{
    Task<CreateRequestResult> CreateRequestAsync(RequestDto request, ISender sender,
        CancellationToken cancellation);

    Task<CreateDraftRequestResult> CreateRequestDraftAsync(RequestDto request, ISender sender,
        CancellationToken cancellation);

    Task<UpdateRequestResult> UpdateRequestAsync(RequestDto request, ISender sender,
        CancellationToken cancellation);

    Task<UpdateDraftRequestResult> UpdateRequestDraftAsync(RequestDto request, ISender sender,
        CancellationToken cancellation);
}
