namespace Reporting.Application.Models.Sections;

/// <summary>
/// One Sale Adjustment Grid (SG) or Direct Comparison (DC) table, laid out like the pricing screen.
/// <see cref="Rows"/> is the flat print order (bands, factor rows, price rows, ...); every cell is pre-formatted.
/// The section is absent from the report when the appraisal has no SaleGrid or DirectComparison method.
/// </summary>
public sealed class SaleGridSection
{
    /// <summary>PropertyGroups.GroupNumber for the group this method belongs to.</summary>
    public int GroupNumber { get; init; }

    /// <summary>PropertyGroups.GroupName for the group, or null when no name is set.</summary>
    public string? GroupName { get; init; }

    /// <summary>Which approach / role this page belongs to and whether it is a reference (printed in the page banner).</summary>
    public PricingMethodContext? Context { get; init; }

    /// <summary>True for Direct Comparison (การเปรียบเทียบตรง): no weight band, final row is มูลค่าสุทธิ (ต่ำสุด).</summary>
    public bool IsDirectComparison { get; init; }

    /// <summary>"ตลาด n" + kind · date · unit per comparable, ordered by PricingComparableLinks.DisplaySequence.</summary>
    public IReadOnlyList<MarketHeader> ComparableHeaders { get; init; } = [];

    /// <summary>The comparable columns split into tables that fit the page.</summary>
    public IReadOnlyList<TableChunk> Chunks { get; init; } = [];

    /// <summary>Every row in print order.</summary>
    public IReadOnlyList<SaleGridRow> Rows { get; init; } = [];

    /// <summary>Index in <see cref="Rows"/> of the "การปรับตามปัจจัย และสรุป" band: rows before it print in the first table, it and the rest in a second table with its own header. -1 when absent.</summary>
    public int AdjustStart { get; init; } = -1;

    /// <summary>ปรับมูลค่าสุดท้าย (Adjust Final Value) — from PricingFinalValues. Empty when the method has none.</summary>
    public IReadOnlyList<PricingFinalRow> FinalRows { get; init; } = [];
}

/// <summary>
/// One row of the table. <see cref="Kind"/> picks the layout:
/// "row" label | collateral cell | per comparable (Values + Amounts as two cells, or Values alone across both);
/// "band" a full-width title; "subh" the sub-header of the adjust band; "final" label | collateral value | blank.
/// </summary>
public sealed class SaleGridRow
{
    public string Kind { get; init; } = "row";

    public string? Label { get; init; }

    /// <summary>Per comparable, in <see cref="SaleGridSection.ComparableHeaders"/> order. Null element = blank / "-".</summary>
    public IReadOnlyList<string?> Values { get; init; } = [];

    /// <summary>Second cell per comparable (ระดับ in the factor band, จำนวนเงิน in the adjust band); null = one cell spanning both.</summary>
    public IReadOnlyList<string?>? Amounts { get; init; }

    /// <summary>The ทรัพย์ (Collateral) column: the property value, the factor remark, or the final value.</summary>
    public string? SubjectValue { get; init; }

    /// <summary>True when an empty collateral / value cell should print "-" (factor and adjust bands); price rows leave it blank.</summary>
    public bool DashEmpty { get; init; }

    /// <summary>Price-band rows only: printed in the (otherwise blank) collateral column — บาท / บาท/ตร.วา / บาท/ตร.ม. / % / ปี.</summary>
    public string? Unit { get; init; }

    public bool Emphasis { get; init; }

    /// <summary>
    /// True when at least one comparable cell holds text (a factor value such as a comma-separated list, not a plain figure). Such a row must
    /// wrap; only rows of figures keep the one-line <c>.num</c> cell.
    /// </summary>
    public bool ValuesAreText => TextFactor || Values.Any(IsText);

    /// <summary>A factor row whose factor is not numeric: its cells are text even when every one is empty (the "-" then aligns with the text).</summary>
    public bool TextFactor { get; init; }

    /// <summary>True when the collateral cell holds text (a factor value or the remark of the adjustment band), so it must wrap too.</summary>
    public bool SubjectIsText => IsText(SubjectValue);

    private static bool IsText(string? v) => !string.IsNullOrWhiteSpace(v)
        && !decimal.TryParse(v.Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _);
}
