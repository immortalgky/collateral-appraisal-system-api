using Shared.Messaging.Events;

namespace Request.Application.Features.Requests.CreateRequest;

public class CreateRequestCommandHandler(
    ICreateRequestService createRequestService,
    IIntegrationEventOutbox outbox
) : ICommandHandler<CreateRequestCommand, CreateRequestResult>
{
    public async Task<CreateRequestResult> Handle(CreateRequestCommand command, CancellationToken cancellationToken)
    {
        // Resolution (employee code → full profile + snapshot) happens once inside CreateRequestService.
        var createRequestData = new CreateRequestData(
            Purpose: command.Purpose,
            Channel: command.Channel,
            Creator: command.Creator,
            Priority: command.Priority,
            IsPma: command.IsPma,
            Detail: command.Detail,
            Customers: command.Customers,
            Properties: command.Properties,
            Titles: command.Titles,
            Documents: command.Documents,
            Comments: command.Comments,
            RequestorEmployeeId: command.RequestorEmployeeId);

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
