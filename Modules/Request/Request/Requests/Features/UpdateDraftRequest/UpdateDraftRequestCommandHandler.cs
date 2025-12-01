using Request.Extensions;

namespace Request.Requests.Features.UpdateDraftRequest;

internal class UpdateDraftRequestCommandHandler(IRequestRepository requestRepository)
    : ICommandHandler<UpdateDraftRequestCommand, UpdateDraftRequestResult>
{
    public async Task<UpdateDraftRequestResult> Handle(UpdateDraftRequestCommand command,
        CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.Id, cancellationToken);
        if (request is null)
        {
            throw new RequestNotFoundException(command.Id);
        }

        request.UpdateRequest(
            command.Purpose,
            command.SourceSystem.ToDomain(),
            command.Priority,
            command.IsPMA,
            command.Detail.ToDomain(),
            command.Customers.Select(c => c.ToDomain()).ToList(),
            command.Properties.Select(p => p.ToDomain()).ToList()
        );
        await requestRepository.SaveChangesAsync(cancellationToken);

        return new UpdateDraftRequestResult(true);
    }
}
