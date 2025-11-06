namespace Request.Requests.Features.UpdateRequest;

public record UpdateRequestCommand(
    Guid Id,
    bool HasAppraisalBook,
    Guid? PreviousAppraisaId,
    ReferenceDto Reference,
    LoanDetailDto LoanDetail,
    AddressDto Address,
    ContactDto Contact,
    AppointmentDto Appointment,
    FeeDto Fee,
    RequestorDto Requestor,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties
) : ICommand<UpdateRequestResult>;