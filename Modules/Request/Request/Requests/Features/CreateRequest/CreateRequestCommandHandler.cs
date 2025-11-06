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
            command.Priority,
            RequestStatus.Draft,
            command.IsPMA,
            command.HasOwnAppraisalBook,
            command.PreviousAppraisalId,
            command.LoanDetail.ToDomain(),
            command.Address.ToDomain(),
            command.Contact.ToDomain(),
            Appointment.Create(DateTime.Now, command.Appointment.AppointmentLocation),
            command.Fee.ToDomain(),
            command.Customers.Select(customer => customer.ToDomain()).ToList(),
            command.Properties.Select(property => property.ToDomain()).ToList()
        );

        await requestRepository.CreateRequestAsync(request, cancellationToken);

        return new CreateRequestResult(request.Id);
    }

    private static Models.Request CreateNewRequest(
        string purpose,
        string priority,
        RequestStatus requestStatus,
        bool isPMA,
        bool hasOwnAppraisalBook,
        Guid? previousAppraisalId,
        LoanDetail loanDetail,
        Address address,
        Contact contact,
        Appointment appointment,
        Fee fee,
        List<RequestCustomer> customers,
        List<RequestProperty> properties
    )
    {
        var request = Models.Request.Create(
            purpose,
            priority,
            requestStatus,
            isPMA,
            hasOwnAppraisalBook,
            previousAppraisalId,
            loanDetail,
            address,
            contact,
            appointment,
            fee
        );

        // Add customers
        customers.ForEach(c => request.AddCustomer(c.Name, c.ContactNumber));

        // Add properties
        properties.ForEach(p => request.AddProperty(p.PropertyType, p.BuildingType, p.SellingPrice));

        return request;
    }
}