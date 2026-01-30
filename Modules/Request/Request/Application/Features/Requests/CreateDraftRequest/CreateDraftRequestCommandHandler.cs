namespace Request.Application.Features.Requests.CreateDraftRequest;

internal class CreateDraftRequestCommandHandler(
    ICreateRequestService createRequestService
) : ICommandHandler<CreateDraftRequestCommand, CreateDraftRequestResult>
{
    public async Task<CreateDraftRequestResult> Handle(
        CreateDraftRequestCommand command,
        CancellationToken cancellationToken)
    {
        var createRequestData = command.Adapt<CreateRequestData>();

        var request = await createRequestService.CreateRequestAsync(createRequestData, cancellationToken);

        return new CreateDraftRequestResult(request.Id);
    }
}