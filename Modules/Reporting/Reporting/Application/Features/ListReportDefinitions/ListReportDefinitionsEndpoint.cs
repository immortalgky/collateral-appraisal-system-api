using Microsoft.EntityFrameworkCore;
using Reporting.Data;

namespace Reporting.Application.Features.ListReportDefinitions;

/// <summary>
/// GET /reports/definitions
///
/// Returns all enabled report definitions so the FE can discover available reports
/// without a code change. Disabled definitions are excluded.
///
/// Auth: login-only (.RequireAuthorization() — no specific permission policy).
/// </summary>
public sealed class ListReportDefinitionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/definitions", HandleAsync)
            .RequireAuthorization()
            .WithTags("Reports")
            .WithName("ListReportDefinitions")
            .WithSummary("List all enabled report definitions")
            .Produces<List<ReportDefinitionDto>>(200)
            .Produces(401);
    }

    private static async Task<IResult> HandleAsync(
        ReportingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var definitions = await dbContext.ReportDefinitions
            .AsNoTracking()
            .Where(d => d.IsEnabled)
            .OrderBy(d => d.Category)
            .ThenBy(d => d.ReportTypeKey)
            .Select(d => new ReportDefinitionDto(
                d.ReportTypeKey,
                d.DisplayNameTh,
                d.DisplayNameEn,
                d.Category,
                d.GenerationMode.ToString()))
            .ToListAsync(cancellationToken);

        return Results.Ok(definitions);
    }
}

/// <summary>DTO returned by <see cref="ListReportDefinitionsEndpoint"/>.</summary>
public sealed record ReportDefinitionDto(
    string ReportTypeKey,
    string DisplayNameTh,
    string DisplayNameEn,
    string Category,
    string GenerationMode);
