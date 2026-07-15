using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Monitoring.GetPendingQuotations;

public class GetPendingQuotationsSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/quotations/summary",
                async (
                    string[]? status,
                    string? quotationNo,
                    string? appraisalNo,
                    string? customerName,
                    string? appraisalCompanyId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new PendingQuotationFilter(status, quotationNo, appraisalNo, customerName, null, null, null, null, appraisalCompanyId);
                    var result = await sender.Send(new GetPendingQuotationsSummaryQuery(filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingQuotationsSummary")
            .Produces<MonitoringSummaryDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending Quotations summary KPIs")
            .WithDescription("Returns Total count for the Pending Quotations tab. Bucket fields (Breached/AtRisk/Healthy) are null — the quotation view does not expose OLA columns. Requires any MONITORING:PENDING_QUOTATION* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingQuotation);
    }
}
