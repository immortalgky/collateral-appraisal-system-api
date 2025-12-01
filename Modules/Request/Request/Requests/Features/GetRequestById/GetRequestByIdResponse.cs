namespace Request.Requests.Features.GetRequestById;

public record GetRequestByIdResponse(
    Guid Id,
    string? AppraisalNo,
    string? Purpose,
    string Priority,
    SourceSystemDto SourceSystem,
    string Status,
    bool IsPMA,
    RequestDetailDto Detail,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties);