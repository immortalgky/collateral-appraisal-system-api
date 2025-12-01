namespace Request.Requests.Features.DeleteRequest;

internal class DeleteRequestCommandHandler(IRequestRepository requestRepository)
    : ICommandHandler<DeleteRequestCommand, DeleteRequestResult>
{
    public async Task<DeleteRequestResult> Handle(DeleteRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.Id, cancellationToken);

        request.UpdateIsDelete();

        await requestRepository.SaveChangesAsync(cancellationToken);

        return new DeleteRequestResult(true);
    }
}