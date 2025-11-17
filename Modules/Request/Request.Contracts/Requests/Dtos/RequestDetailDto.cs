namespace Request.Contracts.Requests.Dtos;

public record RequestDetailDto(
    bool HasAppraisalBook,
    LoanDetailDto LoanDetail,
    long? PrevAppraisalNo,
    AddressDto Address,
    ContactDto Contact,
    AppointmentDto Appointment,
    FeeDto Fee
    // RequestorDto Requestor
);