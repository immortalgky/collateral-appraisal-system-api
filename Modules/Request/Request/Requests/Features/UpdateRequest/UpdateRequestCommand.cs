namespace Request.Requests.Features.UpdateRequest;

public record UpdateRequestCommand(
    Guid Id,
    RequestDetailDto Detail,
    bool IsPMA,
    string Purpose,
    string Priority,
    SourceSystemDto SourceSystem,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties
) : ICommand<UpdateRequestResult>;