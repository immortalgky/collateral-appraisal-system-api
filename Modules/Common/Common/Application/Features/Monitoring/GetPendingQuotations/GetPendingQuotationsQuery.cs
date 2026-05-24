using Shared.CQRS;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingQuotations;

public record GetPendingQuotationsQuery(
    PaginationRequest Paging,
    PendingQuotationFilter Filter
) : IQuery<PaginatedResult<PendingQuotationDto>>;
