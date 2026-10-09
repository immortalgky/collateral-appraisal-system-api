using System.Globalization;
using System.Text;

namespace Reporting.Application.Formatting;

/// <summary>
/// Builds the FSD's standard Thai collateral-address strings. Used by report data
/// providers (not templates) so the formatting rule lives in one place.
///
/// Land / Building (FSD §2.1.2.5, summary field 7):
///   เลขที่ 99/172 หมู่บ้านสุขสันต์ ม.2 ซอย ไม่มีชื่อ ถนน ทางหลวงชนบท
///   ตำบล/แขวง หินเหล็กไฟ อำเภอ/เขต คูเมือง จังหวัดบุรีรัมย์
///
/// Condo (FSD §2.1.2.2 field 7, summary condo field 7):
///   ห้องชุดเลขที่ 176/120 ชั้นที่ 7 อาคารชุดเออร์บาโน ราชวิถี ซอย ราชวิถี 12
///   ถนน ราชวิถี ตำบล/แขวง บางพลัด อำเภอ/เขต บางพลัด จังหวัดกรุงเทพมหานคร
///
/// Any blank segment is omitted so the output never shows dangling labels.
/// Returns "" when nothing is supplied.
/// </summary>
public static class ThaiAddressFormatter
{
    /// <summary>
    /// Formats GPS as the FSD's "N 13.270399 E 100.94802" string. The hemisphere is
    /// derived from the sign (N/S latitude, E/W longitude) using the absolute value,
    /// so any negative/bad coordinate degrades correctly rather than mislabelling.
    /// Thai collateral is always N/E in practice. Returns null when either side is missing.
    /// </summary>
    public static string? FormatGps(decimal? latitude, decimal? longitude)
    {
        if (latitude is not { } lat || longitude is not { } lon)
            return null;

        var ns = lat >= 0 ? "N" : "S";
        var ew = lon >= 0 ? "E" : "W";
        return $"{ns} {Math.Abs(lat):F6} {ew} {Math.Abs(lon):F6}";
    }

    public static string FormatLandBuilding(
        string? houseNumber,
        string? village,
        string? moo,
        string? soi,
        string? road,
        string? subDistrict,
        string? district,
        string? province)
    {
        var sb = new StringBuilder();
        Append(sb, "เลขที่", houseNumber);
        Append(sb, null, village);
        Append(sb, "ม.", moo, spaceAfterLabel: false);
        Append(sb, "ซอย", soi);
        Append(sb, "ถนน", road);
        Append(sb, "ตำบล/แขวง", subDistrict);
        Append(sb, "อำเภอ/เขต", district);
        Append(sb, "จังหวัด", province, spaceAfterLabel: false);
        return sb.ToString().Trim();
    }

    public static string FormatCondo(
        string? roomNumber,
        string? floorNumber,
        string? buildingName,
        string? soi,
        string? road,
        string? subDistrict,
        string? district,
        string? province)
    {
        var sb = new StringBuilder();
        Append(sb, "ห้องชุดเลขที่", roomNumber);
        Append(sb, "ชั้นที่", floorNumber);
        Append(sb, null, buildingName);
        Append(sb, "ซอย", soi);
        Append(sb, "ถนน", road);
        Append(sb, "ตำบล/แขวง", subDistrict);
        Append(sb, "อำเภอ/เขต", district);
        Append(sb, "จังหวัด", province, spaceAfterLabel: false);
        return sb.ToString().Trim();
    }

    // Hyphen-minus, en/em dash, and the minus sign / hyphens / fullwidth forms that pasted or IME
    // text brings in.
    private static readonly char[] Dashes = ['-', '–', '—', '−', '‐', '‑', '‒', '－', '﹣'];

    /// <summary>
    /// The trimmed value, or null when it is blank or only dashes (the placeholders for "no value")
    /// — so a segment and its label drop out instead of printing "ซอย -".
    /// </summary>
    public static string? Stated(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.All(c => char.IsWhiteSpace(c) || Dashes.Contains(c)) ? null : trimmed;
    }

    /// <summary>
    /// Whether a free-text numeric field (e.g. a condo floor) was actually filled in. Blank or dashes
    /// mean not stated; so does a numeric zero, which the form cannot tell apart from "not stated" and
    /// which would otherwise print as a fact ("ชั้นที่ 0"). Non-numeric text (e.g. "G") counts as stated.
    /// </summary>
    public static bool IsStated(string? value) =>
        // Strip the dash placeholders before parsing, so "–0" or "0 -" still read as zero.
        Stated(value)?.Trim(Dashes).Trim() is { } core
        && (!decimal.TryParse(core, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) || n != 0m);

    private static void Append(StringBuilder sb, string? label, string? value, bool spaceAfterLabel = true)
    {
        if (Stated(value) is not { } stated)
            return;

        if (sb.Length > 0)
            sb.Append(' ');

        if (!string.IsNullOrEmpty(label))
        {
            sb.Append(label);
            if (spaceAfterLabel)
                sb.Append(' ');
        }

        sb.Append(stated);
    }
}
