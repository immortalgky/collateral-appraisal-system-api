using Carter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.OperationalReports.Shared;
using Shared.Pagination;

namespace Reporting.Application.OperationalReports.Rcas001;

/// <summary>
/// RCAS001 — Appraisal books by period / status / department.
///   GET /reports/operational/rcas001          → paginated JSON preview
///   GET /reports/operational/rcas001/export    → xlsx | csv | pdf file download
/// Auth: login-only per project convention for new report endpoints.
/// </summary>
public sealed class Rcas001Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports/operational/rcas001")
            .RequireAuthorization()
            .WithTags("Operational Reports");

        group.MapGet("", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo,
                [FromQuery] string? status, [FromQuery] string? bankingSegment,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir,
                [FromQuery] int? pageNumber, [FromQuery] int? pageSize,
                IOperationalReportRunner runner, CancellationToken ct) =>
            {
                var filter = new Rcas001Filter(createdFrom, createdTo, status, bankingSegment, sortBy, sortDir);
                var page = new PaginationRequest(pageNumber ?? 0, pageSize ?? 20);
                return Results.Ok(await runner.PreviewAsync(Rcas001Report.Definition, filter, page));
            })
            .WithName("GetRcas001Preview").WithSummary("RCAS001 paginated preview");

        group.MapGet("/export", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo,
                [FromQuery] string? status, [FromQuery] string? bankingSegment,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir,
                [FromQuery] string? format,
                IOperationalReportRunner runner, CancellationToken ct) =>
            {
                var filter = new Rcas001Filter(createdFrom, createdTo, status, bankingSegment, sortBy, sortDir);
                var file = await runner.ExportAsync(Rcas001Report.Definition, filter, OperationalReportFormat.Parse(format), ct);
                return Results.File(file.Bytes, file.ContentType, file.FileName);
            })
            .WithName("ExportRcas001").WithSummary("RCAS001 export (xlsx | csv | pdf)");
    }
}
