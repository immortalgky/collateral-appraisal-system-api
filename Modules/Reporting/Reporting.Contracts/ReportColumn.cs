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
public sealed record ReportColumn<T>(
    string Header,
    Func<T, object?> Value,
    ColumnFormat Format = ColumnFormat.Text,
    bool Total = false);
