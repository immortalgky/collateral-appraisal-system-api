using Request.Contracts.RequestDocuments.Dto;

namespace Request.Contracts.Requests.Dtos;

public record CreateRequestData(
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
    List<RequestDocumentDto>? Documents,
    List<RequestCommentDto>? Comments
);

