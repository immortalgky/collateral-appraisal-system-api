using Shared.Identity;

namespace Request.Application.Features.Requests.DeleteRequest;

internal class DeleteRequestCommandHandler(
    IRequestRepository requestRepository,
    IDateTimeProvider dateTimeProvider,
    ICurrentUserService currentUserService
) : ICommandHandler<DeleteRequestCommand, DeleteRequestResult>
{
    public async Task<DeleteRequestResult> Handle(DeleteRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.Id, cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.Id);

        request.Delete(currentUserService.Username ?? "anonymous", dateTimeProvider.Now);

        return new DeleteRequestResult(true);
    }
}