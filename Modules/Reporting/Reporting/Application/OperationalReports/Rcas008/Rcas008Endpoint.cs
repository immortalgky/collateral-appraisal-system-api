using Carter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.OperationalReports.Shared;
using Shared.Pagination;

namespace Reporting.Application.OperationalReports.Rcas008;

/// <summary>RCAS008 — external company service quality. Preview + xlsx/csv/pdf export. Login-only.</summary>
public sealed class Rcas008Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports/operational/rcas008")
            .RequireAuthorization().WithTags("Operational Reports");

        group.MapGet("", async (
                [FromQuery] DateTime? approvedFrom, [FromQuery] DateTime? approvedTo,
                [FromQuery] string? bankingSegment, [FromQuery] string? appraisalCompany,
                [FromQuery] string? evaluationStatus, [FromQuery] string? sortBy, [FromQuery] string? sortDir,
                [FromQuery] int? pageNumber, [FromQuery] int? pageSize,
                IOperationalReportRunner runner, CancellationToken ct) =>
            {
                var filter = new Rcas008Filter(approvedFrom, approvedTo, bankingSegment, appraisalCompany, evaluationStatus, sortBy, sortDir);
                var page = new PaginationRequest(pageNumber ?? 0, pageSize ?? 20);
                return Results.Ok(await runner.PreviewAsync(Rcas008Report.Definition, filter, page));
            })
            .WithName("GetRcas008Preview").WithSummary("RCAS008 paginated preview");

        group.MapGet("/export", async (
                [FromQuery] DateTime? approvedFrom, [FromQuery] DateTime? approvedTo,
                [FromQuery] string? bankingSegment, [FromQuery] string? appraisalCompany,
                [FromQuery] string? evaluationStatus, [FromQuery] string? sortBy, [FromQuery] string? sortDir,
                [FromQuery] string? format,
                IOperationalReportRunner runner, CancellationToken ct) =>
            {
                var filter = new Rcas008Filter(approvedFrom, approvedTo, bankingSegment, appraisalCompany, evaluationStatus, sortBy, sortDir);
                var file = await runner.ExportAsync(Rcas008Report.Definition, filter, OperationalReportFormat.Parse(format), ct);
                return Results.File(file.Bytes, file.ContentType, file.FileName);
            })
            .WithName("ExportRcas008").WithSummary("RCAS008 export (xlsx | csv | pdf)");
    }
}
