namespace Request.Application.Features.Requests.CreateRequest;

public class CreateRequestCommandHandler(
    ICreateRequestService createRequestService,
    IBus bus
) : ICommandHandler<CreateRequestCommand, CreateRequestResult>
{
    public async Task<CreateRequestResult> Handle(CreateRequestCommand command, CancellationToken cancellationToken)
    {
        var createRequestData = command.Adapt<CreateRequestData>();

        var request = await createRequestService.CreateRequestAsync(createRequestData, cancellationToken);

        request.Validate();

        if (command.SessionId.HasValue)
        {
            await bus.Publish(
                new SessionCompletedIntegrationEvent(command.SessionId.Value, request.Id),
                cancellationToken);
        }

        return new CreateRequestResult(request.Id);
    }
}