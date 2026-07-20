namespace Reporting.Contracts;

/// <summary>
/// Cross-module file-rendering surface for tabular reports. Implemented in the Reporting module
/// (which owns ClosedXML + the Puppeteer pool); consumed by any module that needs to turn a list
/// of rows into a downloadable Excel / CSV / PDF without referencing those engines.
///
/// Same contract-project pattern as <c>Workflow.Contracts/ISlaCalculatorClient</c>.
/// </summary>
public interface ITabularExporter
{
    /// <summary>
    /// Renders <paramref name="rows"/> through <paramref name="columns"/> into the requested
    /// <paramref name="format"/> and returns the bytes + content type + a timestamped file name
    /// (<c>{baseName}-{yyyyMMdd-HHmmss}.{ext}</c>, stamped in application-local time).
    /// </summary>
    /// <param name="rows">The data rows (already filtered/sorted/capped by the caller).</param>
    /// <param name="columns">Column definitions in display order.</param>
    /// <param name="baseName">File name stem, e.g. <c>"RCAS001-AppraisalBooks"</c>.</param>
    /// <param name="format">Xlsx, Csv, or Pdf.</param>
    /// <param name="title">Optional report title rendered above the table (PDF/Excel).</param>
    /// <param name="appliedFilters">
    /// Optional display-ready filter criteria rendered as an "Applied filters" block under the title,
    /// so a saved/shared file records which selection produced it.
    /// </param>
    /// <param name="signoffs">
    /// Optional sign-off lines rendered as a footer below the table (e.g. Print/Approve Report By).
    /// An entry with a null value renders a blank line to be signed by hand.
    /// </param>
    Task<ReportFile> ExportAsync<T>(
        IReadOnlyList<T> rows,
        IReadOnlyList<ReportColumn<T>> columns,
        string baseName,
        ReportFormat format,
        string? title = null,
        IReadOnlyList<FilterCriterion>? appliedFilters = null,
        IReadOnlyList<ReportSignoff>? signoffs = null,
        CancellationToken cancellationToken = default);
}
