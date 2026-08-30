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
    /// <param name="NumberPrefix">
    /// Overrides the caller's shared prefix for this one item. Land parcels need it because a single
    /// appraisal can mix title-deed types — a โฉนด and a น.ส.3 cannot both be announced as "โฉนดที่ดิน
    /// เลขที่". Leave null to inherit the shared prefix, which is what collateral with one uniform
    /// noun (a condo's "ห้องชุดเลขที่") does.
    /// </param>
    public readonly record struct Item(
        string? Number,
        decimal? Price,
        bool IsMissingFromSurvey,
        int DisplayOrder,
        string? NumberPrefix = null);

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

    /// <summary>
    /// Prefixes a segment with its numbers. Only used when there is more than one segment.
    ///
    /// Items sharing a prefix are announced once and then listed ("โฉนดที่ดินเลขที่ 1234 และ 1235"),
    /// so collateral of a single kind reads exactly as it did before per-item prefixes existed.
    /// Grouping is by prefix, NOT by adjacency: the rows arrive in DisplayOrder, which orders by the
    /// printed collateral list and says nothing about document kind, so kinds can interleave. Runs
    /// would then announce the same noun twice in one segment — the fragmentation this is meant to
    /// avoid. Each group keeps the position of its first item, so the segment still reads in
    /// collateral order.
    /// </summary>
    private static string Describe(IEnumerable<Item> rows, string value, string numberPrefix)
    {
        var numbersByPrefix = new Dictionary<string, List<string>>();
        var prefixOrder = new List<string>();
        foreach (var row in rows.Where(r => !string.IsNullOrWhiteSpace(r.Number)))
        {
            var prefix = string.IsNullOrWhiteSpace(row.NumberPrefix) ? numberPrefix : row.NumberPrefix!;
            if (!numbersByPrefix.TryGetValue(prefix, out var numbers))
            {
                numbers = [];
                numbersByPrefix[prefix] = numbers;
                prefixOrder.Add(prefix);
            }

            numbers.Add(row.Number!);
        }

        if (prefixOrder.Count == 0) return value;

        var described = string.Join(" และ ", prefixOrder
            .Select(prefix => $"{prefix} {string.Join(" และ ", numbersByPrefix[prefix])}"));
        return $"{described} {value}";
    }
}
