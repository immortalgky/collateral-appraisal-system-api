namespace Request.Application.Features.Requests.UpdateRequest;

public record UpdateRequestRequest(
    string Purpose,
    string Channel,
    UserInfoDto Requestor,
    UserInfoDto Creator,
    string Priority,
    bool IsPma,
    RequestDetailDto? Detail,
    List<RequestCustomerDto>? Customers,
    List<RequestPropertyDto>? Properties,
    List<RequestTitleDto>? Titles,
    List<RequestDocumentDto>? Documents
);