namespace Collateral.Application.Features.CollateralMasters.GetBackfillReport;

/// <summary>
/// GET /collateral-masters/admin/backfill-report?status=&amp;page=&amp;pageSize=
/// Admin-only. Returns paginated backfill report rows, filterable by status.
/// </summary>
public class GetBackfillReportEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/collateral-masters/admin/backfill-report",
                async (
                    string? status,
                    int? page,
                    int? pageSize,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetBackfillReportQuery(
                        Status: status,
                        PaginationRequest: new PaginationRequest(page ?? 1, pageSize ?? 20));

                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("GetCollateralBackfillReport")
            .Produces<GetBackfillReportResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Get backfill report (admin)")
            .WithDescription("Paginated list of backfill outcomes. Filter by status: Processed, SkippedMissingKey, Error.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}
