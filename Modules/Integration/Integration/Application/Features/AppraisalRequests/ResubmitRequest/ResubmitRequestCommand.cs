namespace Integration.Application.Features.AppraisalRequests.ResubmitRequest;

using Request;
using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;
using Shared.CQRS;

public record ResubmitRequestCommand(
    Guid RequestId,
    string Purpose,
    string Channel,
    UserInfoDto Requestor,
    UserInfoDto Creator,
    string Priority,
    bool IsPma,
    RequestDetailDto? Detail,
    List<RequestCustomerDto>? Customers,
    List<RequestPropertyDto>? Properties,
    List<RequestTitleDto>? Titles,
    List<RequestDocumentDto>? Documents
) : ICommand<ResubmitRequestResult>, ITransactionalCommand<IRequestUnitOfWork>;