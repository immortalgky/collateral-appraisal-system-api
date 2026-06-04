using Reporting.Application.Services;

namespace Reporting.Application.Features.GenerateReport;

/// <summary>
/// GET /reports/{reportTypeKey}/{entityId}
///
/// Generates a report PDF on-demand and streams it to the caller.
///
/// Query parameters:
///   download=true  → Content-Disposition: attachment (triggers browser Save-As)
///   download=false (default) → Content-Disposition: inline (renders in browser)
///
/// Auth: login-only (.RequireAuthorization() — no specific permission policy per project convention).
/// </summary>
public sealed class GenerateReportEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/{reportTypeKey}/{entityId}", HandleAsync)
            .RequireAuthorization()
            .WithTags("Reports")
            .WithName("GenerateReport")
            .WithSummary("Generate a PDF report for the given entity")
            .Produces(200, contentType: "application/pdf")
            .Produces(404)
            .Produces(401);
    }

    private static async Task<IResult> HandleAsync(
        string reportTypeKey,
        string entityId,
        bool download,
        ReportGenerationService reportService,
        ILogger<GenerateReportEndpoint> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var pdfBytes = await reportService.GenerateAsync(
                reportTypeKey, entityId, cancellationToken);

            var fileName = $"{reportTypeKey}.pdf";

            return Results.File(
                pdfBytes,
                contentType: "application/pdf",
                fileDownloadName: download ? fileName : null,
                enableRangeProcessing: true);
        }
        catch (NotFoundException ex)
        {
            logger.LogInformation("Report not found: {Message}", ex.Message);
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unhandled error generating report {ReportTypeKey}/{EntityId}",
                reportTypeKey, entityId);
            throw; // Let GlobalExceptionHandler handle it
        }
    }
}
