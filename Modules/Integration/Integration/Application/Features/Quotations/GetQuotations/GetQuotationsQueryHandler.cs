using Microsoft.EntityFrameworkCore;
using Shared.CQRS;
using Shared.Data;

namespace Integration.Application.Features.Quotations.GetQuotations;

public class GetQuotationsQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetQuotationsQuery, GetQuotationsResult>
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
