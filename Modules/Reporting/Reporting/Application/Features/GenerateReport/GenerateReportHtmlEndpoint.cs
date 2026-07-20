using Reporting.Application.Services;

namespace Reporting.Application.Features.GenerateReport;

/// <summary>
/// GET /reports/{reportTypeKey}/html/{entityId}
///
/// Generates a report as a self-contained, browser-renderable HTML document (for an in-browser
/// paged.js preview) — the same render pipeline as <see cref="GenerateReportEndpoint"/>'s PDF
/// route, minus the HTML→PDF assembly step. See <see cref="ReportGenerationService.GenerateHtmlAsync"/>
/// for the HTML post-processing (file:// image rewrite, inlined fonts/logo) this route depends on.
///
/// <paramref name="entityId"/> — see <see cref="GenerateReportEndpoint"/> remarks. The route uses a
/// catch-all segment so a MeetingNo's embedded slash is captured intact; the literal "html" segment
/// sits before the catch-all so the route stays unambiguous.
///
/// Auth: login-only (.RequireAuthorization() — no specific permission policy per project convention).
/// </summary>
public sealed class GenerateReportHtmlEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/{reportTypeKey}/html/{*entityId}", HandleAsync)
            .RequireAuthorization()
            .WithTags("Reports")
            .WithName("GenerateReportHtml")
            .WithSummary("Generate a browser-renderable HTML preview for the given entity")
            .Produces(200, contentType: "text/html")
            .Produces(404)
            .Produces(401);
    }

    private static async Task<IResult> HandleAsync(
        string reportTypeKey,
        string entityId,
        ReportGenerationService reportService,
        ILogger<GenerateReportHtmlEndpoint> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var html = await reportService.GenerateHtmlAsync(
                reportTypeKey, entityId, cancellationToken);

            return Results.Content(html, "text/html");
        }
        catch (NotFoundException ex)
        {
            logger.LogInformation("Report not found: {Message}", ex.Message);
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unhandled error generating HTML preview for report {ReportTypeKey}/{EntityId}",
                reportTypeKey, entityId);
            throw; // Let GlobalExceptionHandler handle it
        }
    }
}
