using Carter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.OperationalReports.Shared;
using Shared.Pagination;

namespace Reporting.Application.OperationalReports.Rcas002;

/// <summary>RCAS002 — collateral review-due. Preview + xlsx/csv/pdf export. Login-only.</summary>
public sealed class Rcas002Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports/operational/rcas002")
            .RequireAuthorization().WithTags("Operational Reports");

        group.MapGet("", async (
                [FromQuery] string? reviewType, [FromQuery] string? stage, [FromQuery] string? customerName,
                [FromQuery] string? appraisalNumber,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir,
                [FromQuery] int? pageNumber, [FromQuery] int? pageSize,
                IOperationalReportRunner runner, CancellationToken ct) =>
            {
                var filter = new Rcas002Filter(reviewType, stage, customerName, appraisalNumber, sortBy, sortDir);
                var page = new PaginationRequest(pageNumber ?? 0, pageSize ?? 20);
                return Results.Ok(await runner.PreviewAsync(Rcas002Report.Definition, filter, page));
            })
            .WithName("GetRcas002Preview").WithSummary("RCAS002 paginated preview");

        group.MapGet("/export", async (
                [FromQuery] string? reviewType, [FromQuery] string? stage, [FromQuery] string? customerName,
                [FromQuery] string? appraisalNumber,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] string? format,
                IOperationalReportRunner runner, CancellationToken ct) =>
            {
                var filter = new Rcas002Filter(reviewType, stage, customerName, appraisalNumber, sortBy, sortDir);
                var file = await runner.ExportAsync(Rcas002Report.Definition, filter, OperationalReportFormat.Parse(format), ct);
                return Results.File(file.Bytes, file.ContentType, file.FileName);
            })
            .WithName("ExportRcas002").WithSummary("RCAS002 export (xlsx | csv | pdf)");
    }
}
