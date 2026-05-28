using Shared.CQRS;
using Shared.Pagination;

namespace Common.Application.Features.Logs.SearchLogs;

public record SearchLogsQuery(
    PaginationRequest Paging,
    SearchLogsFilter Filter
) : IQuery<PaginatedResult<LogDto>>;
