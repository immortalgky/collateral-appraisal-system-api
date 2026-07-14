using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingQuotations;

public class GetPendingQuotationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/quotations",
                async (
                    [AsParameters] PaginationRequest pagination,
                    string[]? status,
                    string? quotationNo,
                    string? appraisalNo,
                    string? customerName,
                    string? sortBy,
                    string? sortDir,
                    DateOnly? cutOffTimeFrom,
                    DateOnly? cutOffTimeTo,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new PendingQuotationFilter(status, quotationNo, appraisalNo, customerName, sortBy, sortDir, cutOffTimeFrom, cutOffTimeTo);
                    var result = await sender.Send(new GetPendingQuotationsQuery(pagination, filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingQuotations")
            .Produces<PaginatedResult<PendingQuotationDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending Quotation list")
            .WithDescription("Returns pending quotation rows. Requires any MONITORING:PENDING_QUOTATION* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingQuotation);
    }
}
