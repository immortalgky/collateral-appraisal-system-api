using Carter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.OperationalReports.Shared;
using Shared.Pagination;

namespace Reporting.Application.OperationalReports.Rcas004;

/// <summary>RCAS004 — construction inspection &lt; 100%. Preview + xlsx/csv/pdf export. Login-only.</summary>
public sealed class Rcas004Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports/operational/rcas004")
            .RequireAuthorization().WithTags("Operational Reports");

        group.MapGet("", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo, [FromQuery] string? status,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir,
                [FromQuery] int? pageNumber, [FromQuery] int? pageSize,
                IOperationalReportRunner runner, CancellationToken ct) =>
            {
                var filter = new Rcas004Filter(createdFrom, createdTo, status, sortBy, sortDir);
                var page = new PaginationRequest(pageNumber ?? 0, pageSize ?? 20);
                return Results.Ok(await runner.PreviewAsync(Rcas004Report.Definition, filter, page));
            })
            .WithName("GetRcas004Preview").WithSummary("RCAS004 paginated preview");

        group.MapGet("/export", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo, [FromQuery] string? status,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] string? format,
                IOperationalReportRunner runner, CancellationToken ct) =>
            {
                var filter = new Rcas004Filter(createdFrom, createdTo, status, sortBy, sortDir);
                var file = await runner.ExportAsync(Rcas004Report.Definition, filter, OperationalReportFormat.Parse(format), ct);
                return Results.File(file.Bytes, file.ContentType, file.FileName);
            })
            .WithName("ExportRcas004").WithSummary("RCAS004 export (xlsx | csv | pdf)");
    }
}
