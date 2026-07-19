using Carter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.OperationalReports.Shared;
using Shared.Pagination;

namespace Reporting.Application.OperationalReports.Rcas007;

/// <summary>
/// RCAS007 — SLA summary (Internal &amp; External) and RCAS012 — company follow-up (SLA &gt; 2 days).
/// They share the row, view, and business-day SLA computation (<see cref="SlaReport"/>).
/// Preview + xlsx/csv/pdf export. Login-only.
/// </summary>
public sealed class Rcas007Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        Map(app, "rcas007", "RCAS007-SlaSummary",
            "รายงานสรุปการทำงานตาม SLA ของทั้ง Internal & External", scope: null,
            over2Days: false, retailIbgEarly: false);
        // RCAS012 is scoped to External companies (title "ติดตามงานบริษัทประเมิน") — ⚑ confirm; the FSD
        // criteria list no explicit scope, only the SLA > 2 days hard filter.
        Map(app, "rcas012", "RCAS012-CompanyFollowup",
            "รายงานสรุปการติดตามงานบริษัทประเมิน", scope: "External",
            over2Days: true, retailIbgEarly: true);
    }

    private static void Map(IEndpointRouteBuilder app, string slug, string baseName, string title,
        string? scope, bool over2Days, bool retailIbgEarly)
    {
        var group = app.MapGroup($"/reports/operational/{slug}")
            .RequireAuthorization().WithTags("Operational Reports");

        group.MapGet("", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo, [FromQuery] string? status,
                [FromQuery] string? appraisalCompany, [FromQuery] string? internalStaff,
                [FromQuery] string? appraisalNumber, [FromQuery] string? purpose, [FromQuery] string? customerName,
                [FromQuery] string? externalStaff,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir,
                [FromQuery] int? pageNumber, [FromQuery] int? pageSize,
                IOperationalReportRunner runner, IReportSlaCalculator sla, CancellationToken ct) =>
            {
                var def = SlaReport.Create(baseName, title, scope, sla, over2Days, retailIbgEarly);
                var filter = new Rcas007Filter(createdFrom, createdTo, status, appraisalCompany, internalStaff,
                    appraisalNumber, purpose, customerName, externalStaff, sortBy, sortDir);
                var page = new PaginationRequest(pageNumber ?? 0, pageSize ?? 20);
                return Results.Ok(await runner.PreviewAsync(def, filter, page, ct));
            })
            .WithName($"Get{slug}Preview").WithSummary($"{slug.ToUpperInvariant()} paginated preview");

        group.MapGet("/export", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo, [FromQuery] string? status,
                [FromQuery] string? appraisalCompany, [FromQuery] string? internalStaff,
                [FromQuery] string? appraisalNumber, [FromQuery] string? purpose, [FromQuery] string? customerName,
                [FromQuery] string? externalStaff,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] string? format,
                IOperationalReportRunner runner, IReportSlaCalculator sla, CancellationToken ct) =>
            {
                var def = SlaReport.Create(baseName, title, scope, sla, over2Days, retailIbgEarly);
                var filter = new Rcas007Filter(createdFrom, createdTo, status, appraisalCompany, internalStaff,
                    appraisalNumber, purpose, customerName, externalStaff, sortBy, sortDir);
                var file = await runner.ExportAsync(def, filter, OperationalReportFormat.Parse(format), ct);
                return Results.File(file.Bytes, file.ContentType, file.FileName);
            })
            .WithName($"Export{slug}").WithSummary($"{slug.ToUpperInvariant()} export (xlsx | csv | pdf)");
    }
}
