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
            command.Detail,
            command.IsPMA,
            command.Purpose,
            command.Priority,
            command.SourceSystem,
            command.Customers.Select(customer => customer.ToDomain()).ToList(),
            command.Properties.Select(property => property.ToDomain()).ToList()
        );

        await requestRepository.CreateRequestAsync(request, cancellationToken);

        return new CreateRequestResult(request.Id);
    }

    private static Models.Request CreateNewRequest(
        RequestDetailDto requestDetail,
        bool isPMA,
        string purpose,
        string priority,
        SourceSystemDto sourceSystem,
        List<RequestCustomer> customers,
        List<RequestProperty> properties
    )
    {
        var detail = RequestDetail.Create(
            requestDetail.HasAppraisalBook,
            requestDetail.PrevAppraisalNo,
            requestDetail.LoanDetail.ToDomain(),
            requestDetail.Address.ToDomain(),
            requestDetail.Contact.ToDomain(),
            requestDetail.Appointment.ToDomain(),
            requestDetail.Fee.ToDomain()
        );

        var source = sourceSystem.ToDomain();


        var request = Models.Request.Create(
            detail, isPMA, purpose, priority, source
        );

        // Add customers
        customers.ForEach(c => request.AddCustomer(c.Name, c.ContactNumber));

        // Add properties
        properties.ForEach(p => request.AddProperty(p.PropertyType, p.BuildingType, p.SellingPrice));

        return request;
    }
}