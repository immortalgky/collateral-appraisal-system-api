using Dapper;
using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Shared;

/// <summary>
/// Everything that makes one operational report unique: its file/title, column layout, and how
/// its filter turns into SQL (a WHERE over its dedicated <c>reporting.vw_RCAS0NN_*</c> view) and
/// ORDER BY. The generic <see cref="IOperationalReportRunner"/> turns this into a paginated preview
/// or an Excel/CSV/PDF export — so a new report is just a Row, a Definition, and an endpoint.
/// </summary>
public sealed class ReportDefinition<TRow, TFilter>
{
    /// <summary>File-name stem, e.g. <c>"RCAS001-AppraisalBooks"</c>.</summary>
    public required string BaseName { get; init; }

    /// <summary>Title rendered above the table (Excel/PDF).</summary>
    public required string Title { get; init; }

    /// <summary>Column layout shared by preview and export.</summary>
    public required IReadOnlyList<ReportColumn<TRow>> Columns { get; init; }

    /// <summary>Builds the (parameterised) SQL + parameters for a filter — no ORDER BY.</summary>
    public required Func<TFilter, (string Sql, DynamicParameters Parameters)> Build { get; init; }

    /// <summary>Returns the validated <c>ORDER BY</c> clause for a filter (allow-list + safe default).</summary>
    public required Func<TFilter, string> OrderBy { get; init; }

    /// <summary>
    /// Optional post-query enrichment (mutates the rows in place). Used by OLA reports to compute
    /// business-time segments from workflow CompletedTasks after the SQL projection. Runs on the
    /// current page for preview and on the full result set for export. Rows must be mutable classes.
    /// </summary>
    public Func<IReadOnlyList<TRow>, CancellationToken, Task>? EnrichAsync { get; init; }

    /// <summary>Hard cap on exported rows (preview is always paginated).</summary>
    public int MaxRows { get; init; } = 10_000;

    /// <summary>
    /// Optional: describes the filter as an ordered list of <see cref="FilterField"/>s so the export
    /// can print an "Applied filters" block (curated labels; coded values resolved to descriptions).
    /// Not used by preview (the UI already shows the filters on screen).
    /// </summary>
    public Func<TFilter, IReadOnlyList<FilterField>>? DescribeFilter { get; init; }
}
