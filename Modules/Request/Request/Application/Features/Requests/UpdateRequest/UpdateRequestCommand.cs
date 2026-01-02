namespace Request.Application.Features.Requests.UpdateRequest;

public record UpdateRequestCommand(
    Guid Id,
    string Purpose,
    string Channel,
    UserInfoDto Requestor,
    UserInfoDto Creator,
    string Priority,
    bool IsPma,
    RequestDetailDto Detail,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties,
    List<RequestTitleDto>? Titles,
    List<RequestDocumentDto>? Documents
) : ICommand<UpdateRequestResult>, ITransactionalCommand<IRequestUnitOfWork>;