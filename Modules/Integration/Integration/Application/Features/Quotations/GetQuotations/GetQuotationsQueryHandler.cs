using Shared.CQRS;

namespace Integration.Application.Features.Quotations.GetQuotations;

public class GetQuotationsQueryHandler
    : IQueryHandler<GetQuotationsQuery, GetQuotationsResult>
{
    public async Task<GetQuotationsResult> Handle(
        GetQuotationsQuery query,
        CancellationToken cancellationToken)
    {
        // For now, return empty list as quotation module may not have data yet
        // This will be populated once the quotation module is fully implemented
        return new GetQuotationsResult([]);
    }
}
