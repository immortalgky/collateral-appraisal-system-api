using Request.Contracts.RequestDocuments.Dto;

namespace Request.Requests.Features.UpdateDraftRequest;

public record UpdateDraftRequestRequest(
    Guid Id,
    RequestDetailDto Detail,
    bool IsPMA,
    string Purpose,
    string Priority,
    SourceSystemDto SourceSystem,
    List<RequestCustomerDto>? Customers,
    List<RequestPropertyDto>? Properties,
    List<RequestDocumentDto>? Documents
);
