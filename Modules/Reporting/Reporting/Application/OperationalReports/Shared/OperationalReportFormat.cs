using System.Globalization;
using System.Numerics;
using Microsoft.AspNetCore.Http;
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

/// <summary>
/// Binds the PDF layout query params (<c>orientation</c>, <c>overflow</c>, <c>fontSize</c>) of an export
/// endpoint. Untrusted input: anything unknown or unparseable falls back to the default, never a 400.
/// </summary>
public sealed record PdfLayoutQuery(PdfLayout Value)
{
    // Minimal-API custom binding: declare a `PdfLayoutQuery layout` lambda parameter.
    public static ValueTask<PdfLayoutQuery> BindAsync(HttpContext context) =>
        ValueTask.FromResult(Parse(context.Request.Query["orientation"], context.Request.Query["overflow"],
            context.Request.Query["fontSize"]));

    public static PdfLayoutQuery Parse(string? orientation, string? overflow, string? fontSize)
    {
        var d = PdfLayout.Default;
        var landscape = orientation?.ToLowerInvariant() switch
        {
            "portrait" => false,
            "landscape" => true,
            _ => d.Landscape,
        };
        var mode = overflow?.ToLowerInvariant() switch
        {
            "split" => PdfOverflow.Split,
            "fit" => PdfOverflow.Fit,
            _ => d.Overflow,
        };
        // BigInteger so "99999999999" clamps to the max instead of failing to parse.
        var size = BigInteger.TryParse(fontSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out var px)
            ? (int)BigInteger.Clamp(px, PdfLayout.MinFontSizePx, PdfLayout.MaxFontSizePx)
            : d.FontSizePx;
        return new PdfLayoutQuery(new PdfLayout(landscape, mode, size));
    }
}
