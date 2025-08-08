namespace Request.Requests.Features.DeleteRequest;

internal class DeleteRequestCommandHandler(IRequestRepository requestRepository)
    : ICommandHandler<DeleteRequestCommand, DeleteRequestResult>
{
    public async Task<DeleteRequestResult> Handle(DeleteRequestCommand command, CancellationToken cancellationToken)
    {
        await requestRepository.DeleteRequestAsync(command.Id, cancellationToken);

        return new DeleteRequestResult(true);
    }
}