namespace Reporting.Contracts;

/// <summary>
/// Generates a PDF for the named report and entity synchronously (inline bytes).
/// Implemented in the Reporting module over <c>ReportGenerationService</c>;
/// consumed by the Notification module's <c>ReportAttachmentResolver</c> to
/// auto-attach generated PDFs to outbound emails.
/// </summary>
public interface IReportPdfGenerator
{
    /// <summary>
    /// Renders the PDF for <paramref name="reportKey"/> and <paramref name="entityId"/>
    /// and returns the result as a <see cref="ReportFile"/> (bytes + MIME + file name).
    /// </summary>
    Task<ReportFile> GenerateAsync(string reportKey, string entityId, CancellationToken ct = default);
}
