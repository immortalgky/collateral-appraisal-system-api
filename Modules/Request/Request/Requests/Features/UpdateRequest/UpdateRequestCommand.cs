namespace Request.Requests.Features.UpdateRequest;

public record UpdateRequestCommand(
    long Id,
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
) : ICommand<UpdateRequestResult>;