using Carter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.OperationalReports.Shared;
using Shared.Pagination;

namespace Reporting.Application.OperationalReports.Ola;

/// <summary>
/// The four OLA reports, which share the same row/columns/enrichment and differ by name/title, an
/// AssignmentType scope, the FSD sort sequence, and (RCAS006) a trimmed column set:
///   RCAS003 — monthly workload (all)        GET /reports/operational/rcas003 (+ /export)
///   RCAS005 — per External company          GET /reports/operational/rcas005 (+ /export)
///   RCAS006 — per Internal staff             GET /reports/operational/rcas006 (+ /export)
///   RCAS011 — detail by RM/AO                GET /reports/operational/rcas011 (+ /export)
/// (RCAS007/012 are now their own SLA reports — see Rcas007/.)
/// Login-only per project convention.
/// </summary>
public sealed class OlaReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // fsdDetail: RCAS003/005/006/011 field tables include Appraisal Create Date + Role and the
        // Print/Approve Report By footer; RCAS007/012 below list none of them.
        Map(app, "rcas003", "RCAS003-MonthlyWorkload",
            "รายงานสรุปปริมาณงาน ประจำเดือน หรือตามช่วงเวลาที่กำหนด", null, fsdDetail: true);
        Map(app, "rcas005", "RCAS005-ExternalCompany",
            "รายงานสรุปตามแต่ละ External Appraisal Company ประจำเดือน", "External",
            fsdDetail: true, defaultSort: ["AppraisalCompany", "AppraisalNumber"]);
        Map(app, "rcas006", "RCAS006-InternalStaff",
            "รายงานสรุปตามแต่ละ Internal Appraisal Staff ประจำเดือน", "Internal",
            fsdDetail: true, defaultSort: ["InternalAppraisalStaff"], internalColumns: true);
        Map(app, "rcas011", "RCAS011-DetailByRm",
            "รายงานรายละเอียดการประเมิน ตาม RM ผู้ดูแลลูกค้า", null, fsdDetail: true);
        // RCAS007/012 are now their own reports (see Rcas007/SlaReport.cs + Rcas007Endpoint.cs) —
        // they no longer alias the OLA base.
    }

    private static void Map(IEndpointRouteBuilder app, string slug, string baseName, string title, string? scope,
        bool fsdDetail = false, IReadOnlyList<string>? defaultSort = null, bool internalColumns = false)
    {
        var group = app.MapGroup($"/reports/operational/{slug}")
            .RequireAuthorization().WithTags("Operational Reports");

        group.MapGet("", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo, [FromQuery] string? status,
                [FromQuery] string? appraisalCompany, [FromQuery] string? internalStaff, [FromQuery] string? channel,
                [FromQuery] string? appraisalNumber, [FromQuery] string? customerName,
                [FromQuery] string? purpose, [FromQuery] string? aoCode,
                [FromQuery] string? externalStaff, [FromQuery] string? departmentCode,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir,
                [FromQuery] int? pageNumber, [FromQuery] int? pageSize,
                IOperationalReportRunner runner, IOlaTimingService ola, CancellationToken ct) =>
            {
                var def = OlaReport.Create(baseName, title, scope, ola, fsdDetail, defaultSort, internalColumns);
                var filter = new OlaFilter(createdFrom, createdTo, status, appraisalCompany, internalStaff, channel,
                    appraisalNumber, customerName, purpose, aoCode, externalStaff, departmentCode, sortBy, sortDir);
                var page = new PaginationRequest(pageNumber ?? 0, pageSize ?? 20);
                return Results.Ok(await runner.PreviewAsync(def, filter, page, ct));
            })
            .WithName($"Get{slug}Preview").WithSummary($"{slug.ToUpperInvariant()} paginated preview");

        group.MapGet("/export", async (
                [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo, [FromQuery] string? status,
                [FromQuery] string? appraisalCompany, [FromQuery] string? internalStaff, [FromQuery] string? channel,
                [FromQuery] string? appraisalNumber, [FromQuery] string? customerName,
                [FromQuery] string? purpose, [FromQuery] string? aoCode,
                [FromQuery] string? externalStaff, [FromQuery] string? departmentCode,
                [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] string? format,
                IOperationalReportRunner runner, IOlaTimingService ola, CancellationToken ct) =>
            {
                var def = OlaReport.Create(baseName, title, scope, ola, fsdDetail, defaultSort, internalColumns);
                var filter = new OlaFilter(createdFrom, createdTo, status, appraisalCompany, internalStaff, channel,
                    appraisalNumber, customerName, purpose, aoCode, externalStaff, departmentCode, sortBy, sortDir);
                var file = await runner.ExportAsync(def, filter, OperationalReportFormat.Parse(format), ct);
                return Results.File(file.Bytes, file.ContentType, file.FileName);
            })
            .WithName($"Export{slug}").WithSummary($"{slug.ToUpperInvariant()} export (xlsx | csv | pdf)");
    }
}
