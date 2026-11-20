namespace Reporting.Application.Formatting;

/// <summary>
/// The one way the book prints a calendar date: day, Thai month name, Buddhist-era year ("28 ตุลาคม 2567").
/// The <c>thai.date</c> template function and every loader that prints a date as text go through here, so a date reads the
/// same in every section and never depends on the server's culture or calendar.
/// </summary>
public static class ThaiDateFormatter
{
    private static readonly string[] ThaiMonths =
    [
        "", "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน",
        "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม"
    ];

    /// <summary>The Thai month name (1 = มกราคม … 12 = ธันวาคม).</summary>
    public static string MonthName(int month) => ThaiMonths[month];

    /// <summary>"28 ตุลาคม 2567".</summary>
    public static string Long(DateTime date) => $"{date.Day} {ThaiMonths[date.Month]} {date.Year + 543}";

    /// <summary>Null stays null, so a missing date prints nothing (or the caller's dash).</summary>
    public static string? Long(DateTime? date) => date is { } d ? Long(d) : null;
}
