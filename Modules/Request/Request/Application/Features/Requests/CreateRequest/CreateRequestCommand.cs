namespace Request.Application.Features.Requests.CreateRequest;

public record CreateRequestCommand(
    Guid? SessionId,
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
    List<RequestDocumentDto>? Documents,
    List<RequestCommentDto>? Comments
) : ICommand<CreateRequestResult>, ITransactionalCommand<IRequestUnitOfWork>;