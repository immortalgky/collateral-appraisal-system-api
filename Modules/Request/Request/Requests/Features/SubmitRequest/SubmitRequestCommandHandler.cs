using System;

namespace Request.Requests.Features.SubmitRequest;

internal class SubmitRequestCommandHandler(IRequestRepository requestRepository)
    : ICommandHandler<SubmitRequestCommand, SubmitRequestResult>
{
    public async Task<SubmitRequestResult> Handle(SubmitRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.Id, cancellationToken);
        if (request is null)
        {
            throw new RequestNotFoundException(command.Id);
        }

        request.Submit();
        await requestRepository.SaveChangesAsync(cancellationToken);

        return new SubmitRequestResult(true);
    }
}
