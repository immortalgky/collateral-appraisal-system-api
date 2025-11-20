using Request.Contracts.RequestDocuments.Dto;

namespace Request.Requests.Features.CreateDraftRequest;

public record CreateDraftRequestRequest(
    Guid SessionId,
    RequestDetailDto Detail,
    bool IsPMA,
    string Purpose,
    string Priority,
    SourceSystemDto SourceSystem,
    List<RequestCustomerDto>? Customers,
    List<RequestPropertyDto>? Properties,
    List<RequestDocumentDto>? Documents
);