using System.Text.Json;

namespace Reporting.Application.Providers;

/// <summary>
/// The two obligation lines the book and the summaries print: the mortgage, and every other obligation.
/// The stored value is the multi-select <c>ObligationType</c> (JSON array of <c>Obligation</c> parameter
/// codes, e.g. <c>["02","03"]</c>); a bare code is read too, so a row the data-fix has not touched still prints.
/// </summary>
internal static class ObligationText
{
    public const string None = "ไม่มี";
    public const string ParameterGroup = "Obligation";

    private const string MortgageCode = "02";
    private const string MortgageDefault = "จำนองเป็นประกัน";

    // Every picked code is an "other" obligation except these: 01 ไม่มีภาระผูกพัน, 02 the mortgage (its own line), and the
    // bit-era "0" that older rows still hold, which is not a code. So a code added to the group later (03 ติดสัญญาเช่า,
    // 04 สิทธิเก็บกิน, 05 สิทธิเหนือพื้นดิน, 06 ภาระจำยอม, 99 อื่นๆ …) prints without a code change here.
    private static readonly HashSet<string> NotOtherCodes = ["01", MortgageCode, "0"];

    /// <summary>ภาระผูกพันการจำนอง: the mortgage text when 02 is picked (its default label when none was typed), else "ไม่มี".</summary>
    public static string Mortgage(string? obligationType, string? mortgageDetails) =>
        Codes(obligationType).Contains(MortgageCode)
            ? Clean(mortgageDetails) ?? MortgageDefault
            : None;

    /// <summary>
    /// ภาระผูกพันอื่นๆ: the typed remark alone when there is one; otherwise the names of every picked code except 01 (none)
    /// and 02 (mortgage), in parameter order, so a picked obligation never prints blank; "ไม่มี" when none of them is picked.
    /// <paramref name="names"/> is the TH description by code; a code with no name prints as itself.
    /// </summary>
    public static string Others(string? obligationType, string? obligationDetails, IReadOnlyDictionary<string, string?> names)
    {
        var picked = AppraisalSummaryCommonLoader
            .OrderCodes(Codes(obligationType).Where(c => !NotOtherCodes.Contains(c)))
            .Select(c => names.TryGetValue(c, out var d) && !string.IsNullOrWhiteSpace(d) ? d! : c)
            .ToList();
        if (picked.Count == 0)
            return None;

        return Clean(obligationDetails) ?? string.Join(", ", picked);
    }

    internal static List<string> Codes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var s = raw.Trim();
        if (!s.StartsWith('['))
            return [s];

        try
        {
            return (JsonSerializer.Deserialize<List<string>>(s) ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
