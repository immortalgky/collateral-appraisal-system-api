namespace Request.Application.Features.Requests.CreateRequest;

public class CreateRequestCommandHandler(
    ICreateRequestService createRequestService
) : ICommandHandler<CreateRequestCommand, CreateRequestResult>
{
    public async Task<CreateRequestResult> Handle(CreateRequestCommand command, CancellationToken cancellationToken)
    {
        var createRequestData = command.Adapt<CreateRequestData>();

        var request = await createRequestService.CreateRequestAsync(createRequestData, cancellationToken);

        request.Validate();

        return new CreateRequestResult(request.Id);
    }
}