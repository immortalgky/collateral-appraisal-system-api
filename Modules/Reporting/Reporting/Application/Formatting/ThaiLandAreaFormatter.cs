namespace Reporting.Application.Formatting;

/// <summary>
/// Formats a total land area as "{rai} - {ngan} - {wa} ไร่ หรือ {totalSqWa} ตารางวา".
/// The rai-ngan-wa triple is normalised from the raw sums (100 sq wa = 1 ngan, 4 ngan = 1 rai)
/// while the trailing total is the absolute area in square wa computed from the RAW sums
/// (1 rai = 400 sq wa, 1 ngan = 100 sq wa) — never from the carry-mutated values, which would
/// double-count. Shared by the appraisal book's cover/letter and the land detail section so the
/// two can never disagree for the same titles.
///
/// This is a PURE formatter — it always returns a string (including "0 - 0 - 0 ไร่ หรือ 0 ตารางวา"
/// for zero input). Each caller owns its own suppression policy (e.g. show the line whenever titles
/// exist vs. hide it when every component is zero), so the shared formatter never changes whether a
/// line appears.
/// </summary>
public static class ThaiLandAreaFormatter
{
    /// <summary>Returns the formatted area string (never null).</summary>
    public static string FormatTotal(decimal sumRai, decimal sumNgan, decimal sumSqWa)
    {
        decimal totalSqWa = Math.Round(sumRai * 400m + sumNgan * 100m + sumSqWa, 2);

        var wa = (int)Math.Round(sumSqWa % 100);
        decimal nganCarried = sumNgan + Math.Floor(sumSqWa / 100);
        var ngan = (int)(nganCarried % 4);
        var rai = (int)(sumRai + Math.Floor(nganCarried / 4));

        return $"{rai} - {ngan} - {wa} ไร่ หรือ {totalSqWa:0.##} ตารางวา";
    }
}
