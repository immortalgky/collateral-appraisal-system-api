namespace Request.Requests.Features.CreateRequest;

public record CreateRequestRequest(
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
    RequestorDto Requestor,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties
);