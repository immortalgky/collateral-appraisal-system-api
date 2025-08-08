using Request.Extensions;

namespace Request.Requests.Features.UpdateRequest;

internal class UpdateRequestCommandHandler(IRequestRepository requestRepository)
    : ICommandHandler<UpdateRequestCommand, UpdateRequestResult>
{
    public async Task<UpdateRequestResult> Handle(UpdateRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.Id, cancellationToken);
        if (request is null)
        {
            throw new RequestNotFoundException(command.Id);
        }

        // Update request details
        request.UpdateDetail(
            command.Purpose,
            command.HasAppraisalBook,
            command.Priority,
            command.Channel,
            command.OccurConstInspec,
            command.Reference.ToDomain(),
            command.LoanDetail.ToDomain(),
            command.Address.ToDomain(),
            command.Contact.ToDomain(),
            command.Fee.ToDomain(),
            command.Requestor.ToDomain()
        );

        // Update customers
        request.UpdateCustomers(command.Customers.Select(c => c.ToDomain()).ToList());

        // Update properties
        request.UpdateProperties(command.Properties.Select(p => p.ToDomain()).ToList());

        await requestRepository.SaveChangesAsync(cancellationToken);

        return new UpdateRequestResult(true);
    }
}