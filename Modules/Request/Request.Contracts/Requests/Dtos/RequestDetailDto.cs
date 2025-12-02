namespace Request.Contracts.Requests.Dtos;

public record RequestDetailDto(
    bool HasAppraisalBook,
    LoanDetailDto? LoanDetail,
    string? PrevAppraisalNo,
    AddressDto? Address,
    ContactDto? Contact,
    AppointmentDto? Appointment,
    FeeDto? Fee
);