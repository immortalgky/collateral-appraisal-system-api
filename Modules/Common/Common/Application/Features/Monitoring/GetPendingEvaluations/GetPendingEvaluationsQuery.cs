using Shared.CQRS;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingEvaluations;

public record GetPendingEvaluationsQuery(
    PaginationRequest Paging,
    PendingEvaluationFilter Filter
) : IQuery<PaginatedResult<PendingEvaluationDto>>;
