namespace Request.Requests.Features.UpdateRequest;

public record UpdateRequestRequest(
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
    List<RequestPropertyDto> Properties,
    List<RequestCommentDto> Comments
);