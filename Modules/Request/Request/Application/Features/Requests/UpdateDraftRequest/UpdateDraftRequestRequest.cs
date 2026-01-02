using Request.Contracts.RequestDocuments.Dto;

namespace Request.Application.Features.Requests.UpdateDraftRequest;

public record UpdateDraftRequestRequest(
    Guid Id,
    Guid SessionId, // Add during sync requestTitles
    RequestDetailDto Detail,
    bool IsPMA,
    string Purpose,
    string Priority,
    SourceSystemDto SourceSystem,
    List<RequestCustomerDto>? Customers,
    List<RequestPropertyDto>? Properties,
    List<RequestDocumentDto>? Documents,
    List<RequestCommentDto> Comments,
    List<RequestTitleDto> Titles
);
