using Request.Extensions;

namespace Request.Requests.Features.UpdateRequest;

internal class UpdateRequestCommandHandler(IRequestRepository requestRepository)
    : ICommandHandler<UpdateRequestCommand, UpdateRequestResult>
{
    public async Task<UpdateRequestResult> Handle(UpdateRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.Id, cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.Id);

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

        return new UpdateRequestResult(true);
    }
}