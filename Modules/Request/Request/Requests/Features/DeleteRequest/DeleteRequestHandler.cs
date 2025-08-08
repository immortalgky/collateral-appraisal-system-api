namespace Request.Requests.Features.DeleteRequest;

internal class DeleteRequestHandler(IRequestRepository requestRepository)
    : ICommandHandler<DeleteRequestCommand, DeleteRequestResult>
{
    public async Task<DeleteRequestResult> Handle(DeleteRequestCommand command, CancellationToken cancellationToken)
    {
        await requestRepository.DeleteRequest(command.Id, cancellationToken);

        return new DeleteRequestResult(true);
    }
}