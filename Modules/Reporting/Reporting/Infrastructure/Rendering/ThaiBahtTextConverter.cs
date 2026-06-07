using System.Globalization;

namespace Reporting.Infrastructure.Rendering;

/// <summary>
/// Converts a monetary amount to its Thai-language reading, e.g.
/// <c>5,610,000.00 → "ห้าล้านหกแสนหนึ่งหมื่นบาทถ้วน"</c>.
///
/// Used by the FSD's recurring "...Value in text" fields (Collateral Value in text,
/// Forced Sale Value in text, etc.). Pure function — unit-testable without Chromium.
///
/// Rules implemented:
///   - digit names ศูนย์..เก้า; place names สิบ ร้อย พัน หมื่น แสน; group word ล้าน (repeats)
///   - tens digit 1 → "สิบ", tens digit 2 → "ยี่สิบ"
///   - unit digit 1 with a higher place present → "เอ็ด" (e.g. 11 → สิบเอ็ด, 101 → หนึ่งร้อยเอ็ด)
///   - 0 baht, 0 satang → "ศูนย์บาทถ้วน"; whole baht → "...บาทถ้วน"; with satang → "...บาท...สตางค์"
///   - negative → prefixed "ลบ"; null → empty string
/// </summary>
public static class ThaiBahtTextConverter
{
    private static readonly string[] Ones =
        { "", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า" };

    // Place value within a 6-digit group: index 0 = units .. 5 = แสน.
    private static readonly string[] Places =
        { "", "สิบ", "ร้อย", "พัน", "หมื่น", "แสน" };

    public static string ToText(decimal? value)
    {
        if (value is null)
            return string.Empty;

        var amount = value.Value;
        var negative = amount < 0;
        amount = Math.Abs(amount);

        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var baht = (long)Math.Truncate(rounded);
        var satang = (int)Math.Round((rounded - baht) * 100m, MidpointRounding.AwayFromZero);

        // Guard the rare rounding edge where satang lands on 100.
        if (satang >= 100)
        {
            baht += 1;
            satang -= 100;
        }

        string text;
        if (satang == 0)
        {
            text = ReadInteger(baht) + "บาทถ้วน";
        }
        else
        {
            var bahtPart = baht > 0 ? ReadInteger(baht) + "บาท" : string.Empty;
            text = bahtPart + ReadInteger(satang) + "สตางค์";
        }

        return negative ? "ลบ" + text : text;
    }

    private static string ReadInteger(long number)
    {
        if (number == 0)
            return "ศูนย์";

        var digits = number.ToString(CultureInfo.InvariantCulture);
        return ReadDigits(digits);
    }

    // Reads an arbitrary-length digit string, grouping by 6 with the repeating "ล้าน".
    private static string ReadDigits(string digits)
    {
        if (digits.Length <= 6)
            return ReadGroup(digits);

        var headLen = digits.Length - 6;
        var head = digits[..headLen];
        var tail = digits[headLen..];

        var tailText = tail.All(c => c == '0') ? string.Empty : ReadGroup(tail);
        return ReadDigits(head) + "ล้าน" + tailText;
    }

    // Reads a group of up to 6 digits (0..999999) into Thai.
    private static string ReadGroup(string group)
    {
        var len = group.Length;
        var sb = new System.Text.StringBuilder();

        for (var i = 0; i < len; i++)
        {
            var digit = group[i] - '0';
            if (digit == 0)
                continue;

            var pos = len - i - 1; // 0 = units .. 5 = แสน

            if (pos == 0 && digit == 1 && len > 1)
                sb.Append("เอ็ด");
            else if (pos == 1 && digit == 1)
                sb.Append("สิบ");
            else if (pos == 1 && digit == 2)
                sb.Append("ยี่สิบ");
            else
                sb.Append(Ones[digit]).Append(Places[pos]);
        }

        return sb.ToString();
    }
}
