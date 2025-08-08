namespace Request.RequestTitles.Features.RemoveRequestTitle;

internal class RemoveRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository)
    : ICommandHandler<RemoveRequestTitleCommand, RemoveRequestTitleResult>
{
    public async Task<RemoveRequestTitleResult> Handle(RemoveRequestTitleCommand command,
        CancellationToken cancellationToken)
    {
        var requestTitle = await requestTitleRepository.GetByIdAsync(command.Id, cancellationToken);
        if (requestTitle is null || requestTitle.RequestId != command.RequestId)
        {
            throw new RequestTitleNotFoundException(command.Id);
        }

        // Publish domain event
        requestTitle.AddDomainEvent(new RequestTitleRemovedEvent(command.RequestId, command.Id,
            requestTitle.CollateralType));

        await requestTitleRepository.Remove(requestTitle);
        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new RemoveRequestTitleResult(true);
    }
}