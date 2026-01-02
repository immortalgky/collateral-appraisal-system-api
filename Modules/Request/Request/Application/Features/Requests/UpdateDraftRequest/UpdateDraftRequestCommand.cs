namespace Request.Application.Features.Requests.UpdateDraftRequest;

public record UpdateDraftRequestCommand(
    Guid Id,
    string? Purpose,
    string? Channel,
    UserInfoDto Requestor,
    UserInfoDto Creator,
    string? Priority,
    bool IsPma,
    RequestDetailDto? Detail,
    List<RequestCustomerDto>? Customers,
    List<RequestPropertyDto>? Properties,
    List<RequestTitleDto>? Titles,
    List<RequestDocumentDto>? Documents
) : ICommand<UpdateDraftRequestResult>, ITransactionalCommand<IRequestUnitOfWork>;