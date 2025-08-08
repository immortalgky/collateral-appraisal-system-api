namespace Request.Requests.Features.CreateRequest;

internal class CreateRequestHandler(
    IRequestRepository requestRepository,
    IAppraisalNumberGenerator appraisalNumberGenerator)
    : ICommandHandler<CreateRequestCommand, CreateRequestResult>
{
    public async Task<CreateRequestResult> Handle(CreateRequestCommand command, CancellationToken cancellationToken)
    {
        var appraisalNumber = await appraisalNumberGenerator.GenerateAsync(cancellationToken);

        var request = CreateNewRequest(appraisalNumber, command);

        await requestRepository.CreateRequest(request, cancellationToken);

        return new CreateRequestResult(request.Id);
    }

    private static Models.Request CreateNewRequest(AppraisalNumber appraisalNumber, CreateRequestCommand command)
    {
        var request = Models.Request.Create(
            appraisalNumber,
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

        command.Customers.ForEach(c => request.AddCustomer(c.Name, c.ContactNumber));

        command.Properties.ForEach(p => request.AddProperty(p.PropertyType, p.BuildingType, p.SellingPrice));

        command.Comments.ForEach(c => request.AddComment(c.Comment));

        return request;
    }
}