namespace Request.Requests.Features.CreateRequest;

public record CreateRequestCommand(
    string Purpose,
    bool HasAppraisalBook,
    string Priority,
    string Channel,
    int? OccurConstInspec,
    ReferenceDto Reference,
    LoanDetailDto LoanDetail,
    AddressDto Address,
    ContactDto Contact,
    FeeDto Fee,
    RequestorDto Requestor,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties,
    List<RequestCommentDto> Comments
) : ICommand<CreateRequestResult>;