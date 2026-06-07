namespace Reporting.Application.Services;

/// <summary>
/// A report provider that does not render its own template, but instead resolves to an
/// ordered list of OTHER report type keys whose PDFs are rendered (each through the normal
/// pipeline) and concatenated into one merged document.
///
/// Used by the unified "appraisal-summary" report, which inspects the appraisal and picks the
/// applicable per-property forms. <see cref="IReportDataProvider.GetModelAsync"/> is never
/// invoked for a composite provider — <see cref="ReportGenerationService"/> short-circuits to
/// this interface before loading any template.
/// </summary>
public interface ICompositeReportProvider
{
    /// <summary>
    /// Ordered child <c>ReportTypeKey</c>s to render and concatenate. An empty list means no
    /// applicable form exists for the entity (the caller turns this into a NotFound).
    /// </summary>
    Task<IReadOnlyList<string>> GetChildReportKeysAsync(
        string entityId,
        CancellationToken cancellationToken);
}
