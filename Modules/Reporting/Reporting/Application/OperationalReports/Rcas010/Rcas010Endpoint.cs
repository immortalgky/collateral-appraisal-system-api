using Carter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.OperationalReports.Shared;
using Shared.Pagination;

namespace Reporting.Application.OperationalReports.Rcas010;

/// <summary>RCAS010 — bank-absorbed fee expense (aggregated). Preview + xlsx/csv/pdf export. Login-only.</summary>
public sealed class Rcas010Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports/operational/rcas010")
            .RequireAuthorization().WithTags("Operational Reports");

        group.MapGet("", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo,
                [FromQuery] string? channel, [FromQuery] string? assignType,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir,
                [FromQuery] int? pageNumber, [FromQuery] int? pageSize,
                IOperationalReportRunner runner, CancellationToken ct) =>
            {
                var filter = new Rcas010Filter(createdFrom, createdTo, channel, assignType, sortBy, sortDir);
                var page = new PaginationRequest(pageNumber ?? 0, pageSize ?? 50);
                return Results.Ok(await runner.PreviewAsync(Rcas010Report.Definition, filter, page));
            })
            .WithName("GetRcas010Preview").WithSummary("RCAS010 paginated preview");

        group.MapGet("/export", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo,
                [FromQuery] string? channel, [FromQuery] string? assignType,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] string? format,
                IOperationalReportRunner runner, CancellationToken ct) =>
            {
                var filter = new Rcas010Filter(createdFrom, createdTo, channel, assignType, sortBy, sortDir);
                var file = await runner.ExportAsync(Rcas010Report.Definition, filter, OperationalReportFormat.Parse(format), ct);
                return Results.File(file.Bytes, file.ContentType, file.FileName);
            })
            .WithName("ExportRcas010").WithSummary("RCAS010 export (xlsx | csv | pdf)");
    }
}
