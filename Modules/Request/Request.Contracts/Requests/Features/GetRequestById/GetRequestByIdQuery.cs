using Shared.CQRS;

namespace Request.Contracts.Requests.Features.GetRequestById;

public record GetRequestByIdQuery(Guid Id) : IQuery<GetRequestByIdResult>;

public record GetRequestByIdResult(
    Guid Id,
    string? AppraisalNo,
    string? Purpose,
    string Priority,
    SourceSystemDto SourceSystem,
    string Status,
    bool IsPMA,
    RequestDetailDto Detail);