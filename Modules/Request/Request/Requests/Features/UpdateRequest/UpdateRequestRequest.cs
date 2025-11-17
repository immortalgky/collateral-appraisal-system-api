namespace Request.Requests.Features.UpdateRequest;

public record UpdateRequestRequest(
    Guid Id,
    RequestDetailDto Detail,
    bool IsPMA,
    string Purpose,
    string Priority,
    SourceSystemDto SourceSystem,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties
);