using Shared.CQRS;
using Shared.Pagination;
using Common.Application.Features.Monitoring.GetPendingInternal;

namespace Common.Application.Features.Monitoring.GetPendingExternal;

public record GetPendingExternalQuery(
    PaginationRequest Paging,
    PendingExternalFilter Filter
) : IQuery<PaginatedResult<PendingTaskDto>>;
