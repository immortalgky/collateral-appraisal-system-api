namespace Request.Requests.Features.UpdateRequest;

internal class UpdateRequestHandler(RequestDbContext dbContext)
    : ICommandHandler<UpdateRequestCommand, UpdateRequestResult>
{
    public async Task<UpdateRequestResult> Handle(UpdateRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await dbContext.Requests.FindAsync([command.Id], cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.Id);

        request.UpdateDetail(
            command.Purpose,
            command.HasAppraisalBook,
            command.Priority,
            command.Channel,
            command.OccurConstInspec,
            command.Reference.Adapt<Reference>(),
            command.LoanDetail.Adapt<LoanDetail>(),
            command.Address.Adapt<Address>(),
            command.Contact.Adapt<Contact>(),
            command.Fee.Adapt<Fee>(),
            command.Requestor.Adapt<Requestor>()
        );

        request.UpdateCustomers(command.Customers.Adapt<List<RequestCustomer>>());
        request.UpdateProperties(command.Properties.Adapt<List<RequestProperty>>());

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateRequestResult(true);
    }
}