namespace Request.Contracts.Requests.Dtos;

public record RequestDetailDto(
    bool HasAppraisalBook,
    LoanDetailDto? LoanDetail,
    Guid? PrevAppraisalId,
    AddressDto? Address,
    ContactDto? Contact,
    AppointmentDto? Appointment,
    FeeDto? Fee
);