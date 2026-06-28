namespace Request.Application.Features.Requests.CreateDraftRequest;

internal class CreateDraftRequestCommandHandler(
    ICreateRequestService createRequestService
) : ICommandHandler<CreateDraftRequestCommand, CreateDraftRequestResult>
{
    public async Task<CreateDraftRequestResult> Handle(
        CreateDraftRequestCommand command,
        CancellationToken cancellationToken)
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

        var (request, _) = await createRequestService.CreateRequestAsync(createRequestData, cancellationToken);

        return new CreateDraftRequestResult(request.Id);
    }
}
