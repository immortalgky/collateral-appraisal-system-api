using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;

namespace Integration.Application.Features.AppraisalRequests.CreateRequest;

public record CreateRequestRequest(
    string? UploadSessionId,
    string Purpose,
    string Channel,
    UserInfoDto Requestor,
    UserInfoDto Creator,
    string Priority,
    bool IsPma,
    RequestDetailDto Detail,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties,
    List<RequestTitleDto> Titles,
    List<RequestDocumentDto> Documents,
    List<RequestCommentDto> Comments,
    string? ExternalCaseKey = null
);

public record CreateRequestResponse(
    Guid RequestId
);