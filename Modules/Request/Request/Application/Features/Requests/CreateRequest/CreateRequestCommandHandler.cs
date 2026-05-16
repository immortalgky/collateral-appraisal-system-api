namespace Request.Application.Features.Requests.CreateRequest;

public class CreateRequestCommandHandler(
    ICreateRequestService createRequestService,
    IIntegrationEventOutbox outbox
) : ICommandHandler<CreateRequestCommand, CreateRequestResult>
{
    public async Task<CreateRequestResult> Handle(CreateRequestCommand command, CancellationToken cancellationToken)
    {
        var createRequestData = command.Adapt<CreateRequestData>();

        var (request, titles) = await createRequestService.CreateRequestAsync(createRequestData, cancellationToken);

        request.Validate();
        foreach (var title in titles) title.Validate();

        request.UpdateStatus(RequestStatus.New);

        if (command.SessionId.HasValue)
            outbox.Publish(
                new SessionCompletedIntegrationEvent(command.SessionId.Value, request.Id),
                request.Id.ToString());

        return new CreateRequestResult(request.Id);
    }
}