namespace Request.Requests.Features.UpdateDraftRequest;

public record UpdateDraftRequestCommand(
    Guid Id,
    RequestDetailDto Detail,
    bool IsPMA,
    string Purpose,
    string Priority,
    SourceSystemDto SourceSystem,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties)
    : ICommand<UpdateDraftRequestResult>;