using Carter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.OperationalReports.Shared;
using Shared.Pagination;

namespace Reporting.Application.OperationalReports.Rcas009;

/// <summary>RCAS009 — appraisal-fee summary. Preview + xlsx/csv/pdf export. Login-only.</summary>
public sealed class Rcas009Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports/operational/rcas009")
            .RequireAuthorization().WithTags("Operational Reports");

        group.MapGet("", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo,
                [FromQuery] string? payType, [FromQuery] string? appraisalCompany, [FromQuery] string? feeStatus,
                [FromQuery] string? appraisalNumber,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir,
                [FromQuery] int? pageNumber, [FromQuery] int? pageSize,
                IOperationalReportRunner runner, CancellationToken ct) =>
            {
                var filter = new Rcas009Filter(createdFrom, createdTo, payType, appraisalCompany, feeStatus, appraisalNumber, sortBy, sortDir);
                var page = new PaginationRequest(pageNumber ?? 0, pageSize ?? 20);
                return Results.Ok(await runner.PreviewAsync(Rcas009Report.Definition, filter, page));
            })
            .WithName("GetRcas009Preview").WithSummary("RCAS009 paginated preview");

        group.MapGet("/export", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo,
                [FromQuery] string? payType, [FromQuery] string? appraisalCompany, [FromQuery] string? feeStatus,
                [FromQuery] string? appraisalNumber,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] string? format,
                IOperationalReportRunner runner, CancellationToken ct) =>
            {
                var filter = new Rcas009Filter(createdFrom, createdTo, payType, appraisalCompany, feeStatus, appraisalNumber, sortBy, sortDir);
                var file = await runner.ExportAsync(Rcas009Report.Definition, filter, OperationalReportFormat.Parse(format), ct);
                return Results.File(file.Bytes, file.ContentType, file.FileName);
            })
            .WithName("ExportRcas009").WithSummary("RCAS009 export (xlsx | csv | pdf)");
    }
}
