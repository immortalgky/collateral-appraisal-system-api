namespace Reporting.Application.Formatting;

/// <summary>
/// Builds the ราคาประเมินราชการ line printed on the appraisal summary forms.
///
/// The rule is the same whatever the collateral is — only the noun ("โฉนดที่ดินเลขที่" for land,
/// "ห้องชุดเลขที่" for condo) and the rate wording ("ตารางวาละ" vs "ตารางเมตรละ") differ — so both
/// providers share this builder rather than each keeping its own copy of the partitioning rules.
/// </summary>
public static class GovernmentPriceTextBuilder
{
    /// <summary>Printed instead of a rate for collateral the appraiser flagged as ตกสำรวจ.</summary>
    public const string MissingFromSurveyText = "ตกสำรวจ";

    /// <summary>
    /// One piece of collateral carrying a government price.
    /// </summary>
    /// <param name="Number">Its title / room number, used only when several segments are printed.</param>
    /// <param name="Price">The government rate, per the unit the caller formats with.</param>
    /// <param name="IsMissingFromSurvey">The appraiser's ตกสำรวจ flag.</param>
    /// <param name="DisplayOrder">
    /// Where this item sits in the printed รายการทรัพย์สิน list, so the segments below read in the
    /// same sequence as the list above them. Items outside every group pass <see cref="int.MaxValue"/>.
    /// </param>
    public readonly record struct Item(
        string? Number,
        decimal? Price,
        bool IsMissingFromSurvey,
        int DisplayOrder);

    /// <summary>
    /// Groups <paramref name="items"/> by identical price and renders one line. With more than one
    /// segment each is prefixed with its numbers ("<paramref name="numberPrefix"/> … และ …") and the
    /// segments are joined by " , "; a lone segment applies to the whole appraisal, so naming the
    /// collateral would be noise. Returns null when there is nothing to print.
    /// </summary>
    /// <param name="numberPrefix">e.g. "โฉนดที่ดินเลขที่" or "ห้องชุดเลขที่".</param>
    /// <param name="formatPrice">e.g. p => $"ตารางวาละ {p:N2} บาท".</param>
    public static string? Build(
        IEnumerable<Item> items,
        string numberPrefix,
        Func<decimal, string> formatPrice)
    {
        var rows = items.ToList();

        // Missing-from-survey collateral has no government valuation, so it reads "ตกสำรวจ" rather
        // than a price. Partition on the FLAG first and never infer it from the price: the frontend
        // only started forcing the price to 0 for flagged rows recently, so older rows carry a real
        // non-zero price alongside the flag. The flag wins.
        var missingFromSurvey = rows
            .Where(r => r.IsMissingFromSurvey)
            .OrderBy(r => r.DisplayOrder)
            .ToList();
        var priceGroups = rows
            .Where(r => !r.IsMissingFromSurvey && r.Price.HasValue)
            .GroupBy(r => r.Price!.Value)
            .ToList();

        var segments = new List<(IReadOnlyList<Item> Rows, string Value)>();
        if (missingFromSurvey.Count > 0)
            segments.Add((missingFromSurvey, MissingFromSurveyText));
        segments.AddRange(priceGroups
            .Select(g => ((IReadOnlyList<Item>)[.. g.OrderBy(r => r.DisplayOrder)], formatPrice(g.Key))));

        // Follow the collateral list, not price order: a segment sits where its first item does.
        segments = segments
            .OrderBy(s => s.Rows.Min(r => r.DisplayOrder))
            .ToList();

        return segments.Count == 0 ? null
            : segments.Count == 1 ? segments[0].Value
            : string.Join(" , ", segments.Select(s => Describe(s.Rows, s.Value, numberPrefix)));
    }

    /// <summary>Prefixes a segment with its numbers. Only used when there is more than one segment.</summary>
    private static string Describe(IEnumerable<Item> rows, string value, string numberPrefix)
    {
        var numbers = string.Join(" และ ", rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Number))
            .Select(r => r.Number));
        return string.IsNullOrWhiteSpace(numbers) ? value : $"{numberPrefix} {numbers} {value}";
    }
}
