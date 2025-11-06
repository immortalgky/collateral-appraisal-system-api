namespace Request.Requests.Features.CreateRequest;

public record CreateRequestRequest(
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
);