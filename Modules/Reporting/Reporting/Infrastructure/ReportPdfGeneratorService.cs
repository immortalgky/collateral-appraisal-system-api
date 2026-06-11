using Reporting.Application.Services;
using Reporting.Contracts;
using Microsoft.Extensions.Logging;

namespace Reporting.Infrastructure;

/// <summary>
/// Implements <see cref="IReportPdfGenerator"/> over the existing <see cref="ReportGenerationService"/>.
/// Generates the PDF synchronously (inline) so the consumer can attach bytes to an email
/// without enqueueing a Hangfire job.
/// </summary>
internal sealed class ReportPdfGeneratorService(
    ReportGenerationService reportGenerationService,
    ILogger<ReportPdfGeneratorService> logger) : IReportPdfGenerator
{
    public async Task<ReportFile> GenerateAsync(string reportKey, string entityId, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Generating PDF for email attachment: report={ReportKey}, entity={EntityId}",
            reportKey, entityId);

        var bytes = await reportGenerationService.GenerateAsync(reportKey, entityId, ct);

        // Recipient-friendly name (no raw Guid), e.g. "meeting-invitation.pdf".
        var fileName = $"{reportKey}.pdf";
        return new ReportFile(bytes, "application/pdf", fileName);
    }
}
