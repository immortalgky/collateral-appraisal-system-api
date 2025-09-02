namespace Shared.Dtos;

public record RequestDetailDto(
    string Purpose,
    bool HasAppraisalBook,
    string Priority,
    ReferenceDto Reference,
    string Channel,
    int? OccurConstInspec,
    LoanDetailDto LoanDetail,
    AddressDto Address,
    ContactDto Contact,
    FeeDto Fee,
    RequestorDto Requestor
);