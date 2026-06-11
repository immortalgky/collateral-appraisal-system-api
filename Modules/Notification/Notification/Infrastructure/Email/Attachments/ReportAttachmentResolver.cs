using Notification.Contracts.Email;
using Reporting.Contracts;

namespace Notification.Infrastructure.Email.Attachments;

/// <summary>
/// Resolves <c>Type="report"</c> attachment refs.
/// The ref value is <c>"reportKey:entityId"</c> (e.g. <c>"meeting-invitation:&lt;meetingId&gt;"</c>).
/// The PDF is generated inline (synchronously) via <see cref="IReportPdfGenerator"/>.
/// Returns an empty list if generation fails.
/// </summary>
internal sealed class ReportAttachmentResolver(
    IReportPdfGenerator pdfGenerator,
    ILogger<ReportAttachmentResolver> logger) : IEmailAttachmentResolver
{
    public string Type => "report";

    public async Task<IReadOnlyList<EmailAttachment>> ResolveAsync(string value, CancellationToken ct)
    {
        var parts = value.Split(':', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            logger.LogWarning(
                "ReportAttachmentResolver: value '{Value}' is not in 'reportKey:entityId' format. Skipping.", value);
            return [];
        }

        var reportKey = parts[0];
        var entityId = parts[1];

        try
        {
            var report = await pdfGenerator.GenerateAsync(reportKey, entityId, ct);
            return [new EmailAttachment(report.FileName, report.Bytes, report.ContentType)];
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "ReportAttachmentResolver: failed to generate PDF for report='{ReportKey}' entity='{EntityId}'. Skipping.",
                reportKey, entityId);
            return [];
        }
    }
}
