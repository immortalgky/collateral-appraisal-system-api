namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "ข้อมูลตลาดเปรียบเทียบในการประเมินมูลค่าหลักทรัพย์"
/// (Comparison Information) section — FSD §2.1.2.8.
///
/// The section is a pivot table: columns are comparables (ข้อมูล 1, ข้อมูล 2, …),
/// rows are factor names (รายละเอียด) plus fixed rows for price and metadata.
///
/// All reference types are nullable so that partial data renders as blank cells.
/// The section is absent from the report when
/// <see cref="Providers.Sections.ComparisonSectionLoader.LoadAsync"/> returns
/// <see langword="null"/> (no comparables linked to this appraisal's pricing methods).
/// </summary>
public sealed class ComparisonSection
{
    /// <summary>
    /// Ordered column headers — one entry per DISTINCT comparable linked across
    /// all pricing methods of this appraisal.
    /// Typically "ข้อมูล 1", "ข้อมูล 2", … derived from <c>DisplaySequence</c>.
    /// </summary>
    public IReadOnlyList<string> ComparableHeaders { get; init; } = [];

    /// <summary>
    /// Data rows — one per factor label plus the fixed trailing rows for
    /// ราคาเสนอขาย, วันที่ลงข้อมูล, and แหล่งที่มา.
    ///
    /// Each row's <see cref="ComparisonFactorRow.Values"/> list is aligned 1-to-1
    /// with <see cref="ComparableHeaders"/>: index <c>i</c> in Values corresponds to
    /// header index <c>i</c>. Missing values are represented as <see langword="null"/>.
    /// </summary>
    public IReadOnlyList<ComparisonFactorRow> Rows { get; init; } = [];
}

/// <summary>
/// A single row in the comparison table.
/// </summary>
public sealed class ComparisonFactorRow
{
    /// <summary>
    /// Row label (factor name in Thai, e.g. "ทำเลที่ตั้ง").
    /// Null for rows where the label is unavailable.
    /// </summary>
    public string? FactorName { get; init; }

    /// <summary>
    /// Cell values — one per comparable, in the same order as
    /// <see cref="ComparisonSection.ComparableHeaders"/>.
    /// Individual cells may be <see langword="null"/> when no data exists.
    /// </summary>
    public IReadOnlyList<string?> Values { get; init; } = [];
}
