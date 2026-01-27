using Mapster;
using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Shared.CQRS;

namespace Integration.Application.Features.AppraisalRequests.CreateRequest;

public class CreateRequestCommandHandler(
    ICreateRequestService createRequestService
) : ICommandHandler<CreateRequestCommand, Guid>
{
    public async Task<Guid> Handle(
        CreateRequestCommand command,
        CancellationToken cancellationToken)
    {
        var createRequestData = command.Adapt<CreateRequestData>();

        var request = await createRequestService.CreateRequestAsync(createRequestData, cancellationToken);

        return request.Id;
    }
}