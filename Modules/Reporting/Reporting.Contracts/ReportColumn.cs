namespace Reporting.Contracts;

/// <summary>
/// Declarative definition of one report column: a header, how to pull the value out of a row,
/// and how to format it. A report is just <c>IReadOnlyList&lt;ReportColumn&lt;T&gt;&gt;</c> over its row type,
/// which the <see cref="ITabularExporter"/> turns into Excel / CSV / PDF.
/// </summary>
/// <typeparam name="T">The report row type.</typeparam>
/// <param name="Header">Column header text (already localized by the caller — EN or TH).</param>
/// <param name="Value">Selector returning the cell value for a row (null renders blank).</param>
/// <param name="Format">Value formatting / typing.</param>
/// <param name="Total">
/// When true, the exporter renders a column total in a footer row (sum for numeric formats,
/// a record count for the first <see cref="ColumnFormat.Text"/> total column).
/// </param>
/// <param name="HeaderTh">Optional Thai header, swapped in for <paramref name="Header"/> on a <c>lang=th</c> export.</param>
/// <param name="FontColor">Optional <c>#rrggbb</c> colour for the data cells in Excel and PDF (CSV ignores it); anything else is ignored.</param>
/// <param name="Center">Centers the header and cells (and total) in Excel and PDF; CSV has no alignment.</param>
/// <param name="Repeat">
/// PDF only: marks an identifier column. It is repeated in every column-set when a wide table is split
/// (when no column is marked, nothing repeats) and kept nowrap, never wrapped or squeezed, in all PDF
/// modes. Do not use it on free-text columns.
/// </param>
public sealed record ReportColumn<T>(
    string Header,
    Func<T, object?> Value,
    ColumnFormat Format = ColumnFormat.Text,
    bool Total = false,
    string? HeaderTh = null,
    string? FontColor = null,
    bool Center = false,
    bool Repeat = false);
