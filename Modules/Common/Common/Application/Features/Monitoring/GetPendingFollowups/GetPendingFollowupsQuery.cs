using Common.Application.Features.Monitoring.GetPendingInternal;
using Shared.CQRS;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingFollowups;

public record GetPendingFollowupsQuery(
    PaginationRequest Paging,
    PendingFollowupFilter Filter
) : IQuery<PaginatedResult<PendingTaskDto>>;
