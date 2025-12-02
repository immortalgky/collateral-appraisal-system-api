using Request.Contracts.RequestDocuments.Dto;

namespace Request.Requests.Features.UpdateRequest;

public record UpdateRequestRequest(
    Guid SessionId,
    RequestDetailDto Detail,
    bool IsPMA,
    string Purpose,
    string Priority,
    SourceSystemDto SourceSystem,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties,
    List<RequestDocumentDto> Documents,
    List<RequestCommentDto> Comments,
    List<RequestTitleDto> Titles
);