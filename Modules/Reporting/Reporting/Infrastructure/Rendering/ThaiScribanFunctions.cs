using System.Globalization;
using Scriban.Runtime;

namespace Reporting.Infrastructure.Rendering;

/// <summary>
/// Exposes a <c>thai</c> helper object to every Scriban template:
///   - <c>{{ thai.baht_text model.collateral_value }}</c> → ห้าล้าน...บาทถ้วน
///   - <c>{{ thai.date model.appraisal_date }}</c>        → 28 ตุลาคม 2567 (Thai month + BE year)
///   - <c>{{ thai.date_short model.request_date }}</c>    → 25/08/2567 (DD/MM/BE)
///
/// All functions are null/blank tolerant (return "") so templates never throw on
/// missing data. Buddhist Era year = Gregorian year + 543.
/// </summary>
internal static class ThaiScribanFunctions
{
    private static readonly string[] ThaiMonths =
    {
        "", "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน",
        "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม"
    };

    public static ScriptObject Create()
    {
        var thai = new ScriptObject();
        thai.Import("baht_text", new Func<object?, string>(BahtText));
        thai.Import("date", new Func<object?, string>(ThaiDateLong));
        thai.Import("date_short", new Func<object?, string>(ThaiDateShort));
        return thai;
    }

    private static string BahtText(object? value) =>
        ThaiBahtTextConverter.ToText(ToDecimal(value));

    // "28 ตุลาคม 2567"
    private static string ThaiDateLong(object? value)
    {
        var d = ToDateTime(value);
        if (d is null)
            return string.Empty;
        var dt = d.Value;
        return $"{dt.Day} {ThaiMonths[dt.Month]} {dt.Year + 543}";
    }

    // "25/08/2567"
    private static string ThaiDateShort(object? value)
    {
        var d = ToDateTime(value);
        if (d is null)
            return string.Empty;
        var dt = d.Value;
        return $"{dt.Day:D2}/{dt.Month:D2}/{dt.Year + 543}";
    }

    private static decimal? ToDecimal(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case decimal dec:
                return dec;
            case double dbl:
                return (decimal)dbl;
            case float flt:
                return (decimal)flt;
            case int or long or short or byte:
                return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            case string s when !string.IsNullOrWhiteSpace(s)
                && decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                return parsed;
            default:
                return null;
        }
    }

    private static DateTime? ToDateTime(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case DateTime dt:
                return dt;
            case DateTimeOffset dto:
                return dto.DateTime;
            case string s when !string.IsNullOrWhiteSpace(s)
                && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed):
                return parsed;
            default:
                return null;
        }
    }
}
