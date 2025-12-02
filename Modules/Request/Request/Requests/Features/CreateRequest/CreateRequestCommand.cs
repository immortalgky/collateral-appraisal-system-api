namespace Request.Requests.Features.CreateRequest;

public record CreateRequestCommand(
    RequestDetailDto Detail,
    bool IsPMA,
    string Purpose,
    string Priority,
    SourceSystemDto SourceSystem,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties
) : ICommand<CreateRequestResult>;
