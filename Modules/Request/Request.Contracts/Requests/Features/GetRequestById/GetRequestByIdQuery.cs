using Shared.Contracts.CQRS;

namespace Request.Contracts.Requests.Features.GetRequestById;

public record GetRequestByIdQuery(long Id) : IQuery<GetRequestByIdResult>;

public record GetRequestByIdResult(
    long Id,
    string? AppraisalNo,
    string Status,
    RequestDetailDto Detail);