using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Shared;

/// <summary>Maps the <c>?format=</c> query string to a <see cref="ReportFormat"/> (default xlsx).</summary>
public static class OperationalReportFormat
{
    public static ReportFormat Parse(string? format) => format?.ToLowerInvariant() switch
    {
        "csv" => ReportFormat.Csv,
        "pdf" => ReportFormat.Pdf,
        _ => ReportFormat.Xlsx,
    };
}
