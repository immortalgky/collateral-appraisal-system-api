using Request.Extensions;

namespace Request.Requests.Features.CreateRequest;

internal class CreateRequestCommandHandler(
    IRequestRepository requestRepository)
    : ICommandHandler<CreateRequestCommand, CreateRequestResult>
{
    public async Task<CreateRequestResult> Handle(CreateRequestCommand command, CancellationToken cancellationToken)
    {
        // Create a request with validated data (appraisal number will be generated in repository)
        var request = CreateNewRequest(
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
            command.Requestor.ToDomain(),
            command.Customers.Select(customer => customer.ToDomain()).ToList(),
            command.Properties.Select(property => property.ToDomain()).ToList()
        );

        await requestRepository.CreateRequestAsync(request, cancellationToken);

        return new CreateRequestResult(request.Id);
    }

    private static Models.Request CreateNewRequest(
        string purpose,
        bool hasAppraisalBook,
        string priority,
        string channel,
        int? occurConstInspec,
        Reference reference,
        LoanDetail loanDetail,
        Address address,
        Contact contact,
        Fee fee,
        Requestor requestor,
        List<RequestCustomer> customers,
        List<RequestProperty> properties
    )
    {
        var request = Models.Request.Create(
            purpose,
            hasAppraisalBook,
            priority,
            channel,
            occurConstInspec,
            reference,
            loanDetail,
            address,
            contact,
            fee,
            requestor
        );

        // Add customers
        customers.ForEach(c => request.AddCustomer(c.Name, c.ContactNumber));

        // Add properties
        properties.ForEach(p => request.AddProperty(p.PropertyType, p.BuildingType, p.SellingPrice));

        return request;
    }
}