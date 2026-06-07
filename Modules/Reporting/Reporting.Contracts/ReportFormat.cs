namespace Reporting.Contracts;

/// <summary>Output format for a tabular operational report.</summary>
public enum ReportFormat
{
    Xlsx,
    Csv,
    Pdf
}

/// <summary>
/// How a column's value is rendered/typed in the output (Excel cell type + number format,
/// CSV text, PDF cell alignment).
/// </summary>
public enum ColumnFormat
{
    Text,
    Integer,
    Decimal,
    Money,

    /// <summary>
    /// The value is already in 0–100 units (e.g. 85.5 means 85.5%), NOT a 0–1 fraction.
    /// Rendered with a literal "%" suffix — do not feed a fraction or it shows 100× too small.
    /// </summary>
    Percent,
    Date,
    DateTime
}
