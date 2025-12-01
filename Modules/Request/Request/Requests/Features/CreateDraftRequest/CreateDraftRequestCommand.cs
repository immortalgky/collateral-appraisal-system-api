namespace Request.Requests.Features.CreateDraftRequest;

public record CreateDraftRequestCommand(
    RequestDetailDto Detail,
    bool IsPMA,
    string Purpose,
    string Priority,
    SourceSystemDto SourceSystem,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties
) : ICommand<CreateDraftRequestResult>;
