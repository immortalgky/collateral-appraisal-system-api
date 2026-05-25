using Shared.CQRS;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingInternal;

public record GetPendingInternalQuery(
    PaginationRequest Paging,
    PendingInternalFilter Filter
) : IQuery<PaginatedResult<PendingTaskDto>>;
