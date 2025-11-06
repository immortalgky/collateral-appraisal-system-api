namespace Request.Requests.Features.CreateRequest;

public record CreateRequestCommand(
    string Purpose,
    string Priority,
    bool IsPMA,
    bool HasOwnAppraisalBook,
    Guid? PreviousAppraisalId,
    LoanDetailDto LoanDetail,
    AddressDto Address,
    ContactDto Contact,
    AppointmentDto Appointment,
    FeeDto Fee,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties
) : ICommand<CreateRequestResult>;