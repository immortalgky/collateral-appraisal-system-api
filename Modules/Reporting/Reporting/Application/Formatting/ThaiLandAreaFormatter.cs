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
        var t = NormalizeTotal(sumRai, sumNgan, sumSqWa);
        return $"{t.Rai:#,##0} - {t.Ngan} - {t.Wa:#,##0.##} ไร่ หรือ {t.TotalSquareWa:#,##0.##} ตารางวา";
    }

    /// <summary>
    /// Normalises the raw rai/ngan/wa sums into a carried rai-ngan-wa triple plus the
    /// absolute total in square wa. Same arithmetic as <see cref="FormatTotal"/> so a
    /// table total row and the formatted string can never disagree.
    /// </summary>
    public static LandAreaTotal NormalizeTotal(decimal sumRai, decimal sumNgan, decimal sumSqWa)
    {
        decimal totalSqWa = Math.Round(
            sumRai * 400m + sumNgan * 100m + sumSqWa, 2, MidpointRounding.AwayFromZero);

        // Decomposed from the single total rather than carried component by component. Each of the
        // three inputs is decimal — AreaRai, AreaNgan and AreaSquareWa all arrive as decimal? from
        // the title rows — so any of them can bring a fraction, and the old carry-by-component
        // arithmetic truncated whichever one it happened to cast. Two rounds of patching it (wa, then
        // ngan) each fixed one component and broke another: correcting the rounding overflow after
        // the ngan carry-down let 2.5 ngan + 60 sq.wa print as "0 - 2 - 110".
        //
        // Dividing the rounded total instead makes the triple correct by construction: wa is a
        // remainder below 100 and ngan a remainder below 4, so neither can overflow, and the triple
        // always reconciles with the "หรือ X ตารางวา" figure on the same line — which is the whole
        // point of this formatter, since an appraiser checks both halves against the deed.
        var rai = (int)Math.Floor(totalSqWa / 400m);
        var afterRai = totalSqWa - rai * 400m;
        var ngan = (int)Math.Floor(afterRai / 100m);
        var wa = afterRai - ngan * 100m;

        return new LandAreaTotal(rai, ngan, wa, totalSqWa);
    }
}

/// <summary>Normalised land-area total: carried rai/ngan/wa + absolute square-wa.</summary>
/// <remarks>
/// <paramref name="Wa"/> is decimal, not int: a deed's square-wa figure carries two decimals and the
/// book is read against the deed.
/// </remarks>
public readonly record struct LandAreaTotal(int Rai, int Ngan, decimal Wa, decimal TotalSquareWa);
