using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.Appraisals.ExportAppraisals;

public class ExportAppraisalsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/export",
                async (
                    // Text search
                    [FromQuery] string? search,
                    // Multi-value filters
                    [FromQuery] string? status,
                    [FromQuery] string? priority,
                    [FromQuery] string? appraisalType,
                    [FromQuery] string? slaStatus,
                    [FromQuery] string? assignmentType,
                    // Assignment (username like "P5229")
                    [FromQuery] string? assigneeUserId,
                    [FromQuery] string? assigneeCompanyId,
                    // Request metadata
                    [FromQuery] string? channel,
                    [FromQuery] string? bankingSegment,
                    [FromQuery] bool? isPma,
                    // Geographic
                    [FromQuery] string? province,
                    [FromQuery] string? district,
                    // Date ranges
                    [FromQuery] DateTime? createdFrom,
                    [FromQuery] DateTime? createdTo,
                    [FromQuery] DateTime? slaDueDateFrom,
                    [FromQuery] DateTime? slaDueDateTo,
                    [FromQuery] DateTime? assignedDateFrom,
                    [FromQuery] DateTime? assignedDateTo,
                    [FromQuery] DateTime? appointmentDateFrom,
                    [FromQuery] DateTime? appointmentDateTo,
                    // Sorting
                    [FromQuery] string? sortBy,
                    [FromQuery] string? sortDir,
                    // Export format: "xlsx" (default) or "csv"
                    [FromQuery] string? format,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var filter = new GetAppraisalsFilterRequest(
                        Search: search,
                        Status: status,
                        Priority: priority,
                        AppraisalType: appraisalType,
                        SlaStatus: slaStatus,
                        AssignmentType: assignmentType,
                        AssigneeUserId: assigneeUserId,
                        AssigneeCompanyId: assigneeCompanyId,
                        Channel: channel,
                        BankingSegment: bankingSegment,
                        IsPma: isPma,
                        Province: province,
                        District: district,
                        CreatedFrom: createdFrom,
                        CreatedTo: createdTo,
                        SlaDueDateFrom: slaDueDateFrom,
                        SlaDueDateTo: slaDueDateTo,
                        AssignedDateFrom: assignedDateFrom,
                        AssignedDateTo: assignedDateTo,
                        AppointmentDateFrom: appointmentDateFrom,
                        AppointmentDateTo: appointmentDateTo,
                        SortBy: sortBy,
                        SortDir: sortDir
                    );

                    var query = new ExportAppraisalsQuery(filter, format ?? "xlsx");
                    var result = await sender.Send(query, cancellationToken);

                    return Results.File(result.FileBytes, result.ContentType, result.FileName);
                }
            )
            .WithName("ExportAppraisals")
            .Produces(StatusCodes.Status200OK)
            .WithSummary("Export appraisals to file")
            .WithDescription(
                "Exports all matching appraisals (up to 10,000 rows) as a file download. " +
                "Accepts the same filter parameters as GET /appraisals. " +
                "Use format=xlsx (default) for Excel or format=csv for CSV with UTF-8 BOM.")
            .WithTags("Appraisal");
    }
}
