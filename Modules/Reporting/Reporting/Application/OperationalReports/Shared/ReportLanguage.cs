using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Shared;

/// <summary>Export language for column headers and applied-filter labels / values.</summary>
public enum ReportLanguage
{
    En,
    Th,
}

/// <summary>Maps the <c>?lang=</c> query string to a <see cref="ReportLanguage"/> (default English).</summary>
public static class OperationalReportLanguage
{
    public static ReportLanguage Parse(string? lang) =>
        string.Equals(lang?.Trim(), "th", StringComparison.OrdinalIgnoreCase) ? ReportLanguage.Th : ReportLanguage.En;

    /// <summary>Thai: <c>HeaderTh ?? Header</c> per column. English: the list unchanged.</summary>
    internal static IReadOnlyList<ReportColumn<T>> Headers<T>(this ReportLanguage language, IReadOnlyList<ReportColumn<T>> columns) =>
        language == ReportLanguage.Th
            ? columns.Select(c => c with { Header = c.HeaderTh ?? c.Header }).ToList()
            : columns;

    /// <summary>Thai: <c>LabelTh ?? Label</c> per filter. English: the list unchanged.</summary>
    internal static IReadOnlyList<FilterField> Labels(this ReportLanguage language, IReadOnlyList<FilterField> fields) =>
        language == ReportLanguage.Th
            ? fields.Select(f => f with { Label = f.LabelTh ?? f.Label }).ToList()
            : fields;
}
