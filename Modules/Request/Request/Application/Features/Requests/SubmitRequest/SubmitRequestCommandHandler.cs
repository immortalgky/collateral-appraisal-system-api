namespace Request.Application.Features.Requests.SubmitRequest;

internal class SubmitRequestCommandHandler(
    IRequestRepository requestRepository,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<SubmitRequestCommand, SubmitRequestResult>
{
    public async Task<SubmitRequestResult> Handle(SubmitRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.Id, cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.Id);

        request.Submit(dateTimeProvider.Now);

        return new SubmitRequestResult(true);
    }
}