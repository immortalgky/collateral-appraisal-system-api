using Carter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.OperationalReports.Shared;
using Shared.Pagination;

namespace Reporting.Application.OperationalReports.Ola;

/// <summary>
/// The four OLA reports, which share the same row/columns/enrichment and differ only by name/title
/// and an AssignmentType scope:
///   RCAS003 — monthly workload (all)        GET /reports/operational/rcas003 (+ /export)
///   RCAS005 — per External company          GET /reports/operational/rcas005 (+ /export)
///   RCAS006 — per Internal staff             GET /reports/operational/rcas006 (+ /export)
///   RCAS011 — detail by RM/AO                GET /reports/operational/rcas011 (+ /export)
/// Login-only per project convention.
/// </summary>
public sealed class OlaReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        Map(app, "rcas003", "RCAS003-MonthlyWorkload",
            "รายงานสรุปปริมาณงาน ประจำเดือน หรือตามช่วงเวลาที่กำหนด", null);
        Map(app, "rcas005", "RCAS005-ExternalCompany",
            "รายงานสรุปตามแต่ละ External Appraisal Company ประจำเดือน", "External");
        Map(app, "rcas006", "RCAS006-InternalStaff",
            "รายงานสรุปตามแต่ละ Internal Appraisal Staff ประจำเดือน", "Internal");
        Map(app, "rcas011", "RCAS011-DetailByRm",
            "รายงานรายละเอียดการประเมิน ตาม RM ผู้ดูแลลูกค้า", null);
        // RCAS007/012 are SLA-focused but share the OLA base + business-time computation.
        // The "OLA Appraisal (hrs)" column is the SLA-relevant elapsed (appointment → company→bank).
        // Phase 3: the exact FSD "SLA setting − elapsed" formula, RCAS012's ">2 days" hard filter,
        // and the phones/fee/value-100%/current-role columns.
        Map(app, "rcas007", "RCAS007-SlaSummary",
            "รายงานสรุปการทำงานตาม SLA ของทั้ง Internal & External", null);
        Map(app, "rcas012", "RCAS012-CompanyFollowup",
            "รายงานสรุปการติดตามงานบริษัทประเมิน", "External");
    }

    private static void Map(IEndpointRouteBuilder app, string slug, string baseName, string title, string? scope)
    {
        var group = app.MapGroup($"/reports/operational/{slug}")
            .RequireAuthorization().WithTags("Operational Reports");

        group.MapGet("", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo, [FromQuery] string? status,
                [FromQuery] string? appraisalCompany, [FromQuery] string? internalStaff, [FromQuery] string? channel,
                [FromQuery] string? appraisalNumber,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir,
                [FromQuery] int? pageNumber, [FromQuery] int? pageSize,
                IOperationalReportRunner runner, IOlaTimingService ola, CancellationToken ct) =>
            {
                var def = OlaReport.Create(baseName, title, scope, ola);
                var filter = new OlaFilter(createdFrom, createdTo, status, appraisalCompany, internalStaff, channel, appraisalNumber, sortBy, sortDir);
                var page = new PaginationRequest(pageNumber ?? 0, pageSize ?? 20);
                return Results.Ok(await runner.PreviewAsync(def, filter, page, ct));
            })
            .WithName($"Get{slug}Preview").WithSummary($"{slug.ToUpperInvariant()} paginated preview");

        group.MapGet("/export", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo, [FromQuery] string? status,
                [FromQuery] string? appraisalCompany, [FromQuery] string? internalStaff, [FromQuery] string? channel,
                [FromQuery] string? appraisalNumber,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] string? format,
                IOperationalReportRunner runner, IOlaTimingService ola, CancellationToken ct) =>
            {
                var def = OlaReport.Create(baseName, title, scope, ola);
                var filter = new OlaFilter(createdFrom, createdTo, status, appraisalCompany, internalStaff, channel, appraisalNumber, sortBy, sortDir);
                var file = await runner.ExportAsync(def, filter, OperationalReportFormat.Parse(format), ct);
                return Results.File(file.Bytes, file.ContentType, file.FileName);
            })
            .WithName($"Export{slug}").WithSummary($"{slug.ToUpperInvariant()} export (xlsx | csv | pdf)");
    }
}
