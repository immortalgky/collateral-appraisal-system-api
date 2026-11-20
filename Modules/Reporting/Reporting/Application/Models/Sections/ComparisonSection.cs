namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "ข้อมูลตลาดเปรียบเทียบในการประเมินมูลค่าหลักทรัพย์"
/// (Comparison Information) section — FSD §2.1.2.8.
///
/// The section is split into one <see cref="ComparisonTable"/> per
/// <c>MarketComparable.PropertyType</c> (ที่ดินเปล่า, ที่ดินและสิ่งปลูกสร้าง, คอนโด, …).
/// Each table is a pivot: columns are comparables (ข้อมูล 1, ข้อมูล 2, …), rows are factor
/// names (รายละเอียด) plus fixed rows for price and metadata.
///
/// The section is absent from the report when
/// <see cref="Providers.Sections.ComparisonSectionLoader.LoadAsync"/> returns
/// <see langword="null"/> (no comparables linked to this appraisal's pricing methods).
/// </summary>
public sealed class ComparisonSection
{
    // ── Property group routing ────────────────────────────────────────────────────

    /// <summary>
    /// PropertyGroups.GroupNumber — 0 when comparison section is not per-group.
    /// Comparison aggregates all groups' comparables into a single section so
    /// GroupNumber defaults to 0 and GroupName to null.
    /// </summary>
    public int GroupNumber { get; init; }

    /// <summary>PropertyGroups.GroupName — null for the shared comparison section.</summary>
    public string? GroupName { get; init; }

    /// <summary>
    /// One table per property-type group, ordered by first appearance
    /// (lowest <c>DisplaySequence</c> of the group's comparables).
    /// </summary>
    public IReadOnlyList<ComparisonTable> Tables { get; init; } = [];

    /// <summary>True when the appraisal has two or more property groups: the tables are headed "กลุ่มที่ N".</summary>
    public bool MultiGroup { get; init; }
}

/// <summary>
/// A single per-property-type comparison table.
/// </summary>
public sealed class ComparisonTable
{
    /// <summary>
    /// Group heading — the Thai description of the property type (resolved from parameter
    /// group <c>PropertyType</c>, e.g. "ที่ดินและสิ่งปลูกสร้าง"). Falls back to the raw
    /// code when no description exists, and may be null when the comparable has no type.
    /// </summary>
    public string? PropertyTypeLabel { get; init; }

    /// <summary>
    /// Ordered column headers — one per comparable in this group, "ข้อมูล 1", "ข้อมูล 2", …
    /// (renumbered per table).
    /// </summary>
    public IReadOnlyList<string> ComparableHeaders { get; init; } = [];

    /// <summary>
    /// Data rows — one per factor present in this group's comparables, plus the fixed
    /// trailing rows for ราคาเสนอขาย, วันที่ลงข้อมูล, and แหล่งที่มา.
    ///
    /// Each row's <see cref="ComparisonFactorRow.Values"/> list is aligned 1-to-1 with
    /// <see cref="ComparableHeaders"/>: index <c>i</c> corresponds to header index <c>i</c>.
    /// Missing values are represented as <see langword="null"/>.
    /// </summary>
    public IReadOnlyList<ComparisonFactorRow> Rows { get; init; } = [];

    /// <summary>
    /// The rows of the FSD layout (2.1.2.8), in FSD order: ประเภททรัพย์สิน … หมายเหตุ. This is what the book prints;
    /// <see cref="Rows"/> keeps every factor and fixed row so more can be switched back on later.
    /// </summary>
    public IReadOnlyList<ComparisonFactorRow> FsdRows { get; init; } = [];

    /// <summary>
    /// Header(s) of the subject column(s) printed right after the label column ("ทรัพย์สินที่ประเมิน", or one
    /// "ทรัพย์สิน กลุ่มที่ N" per property group in a multi-group appraisal). Empty = no subject column.
    /// Each <see cref="ComparisonFactorRow.SubjectValues"/> list lines up with it.
    /// </summary>
    public IReadOnlyList<string> SubjectHeaders { get; init; } = [];

    /// <summary>The property group these comparables belong to (the section lists each group's used comparables).</summary>
    public int GroupNumber { get; init; }

    /// <summary>ภาพถ่าย of the subject column: file:// source of the group's photo; null = empty cell.</summary>
    public string? SubjectPhoto { get; init; }

    /// <summary>
    /// ภาพถ่าย — per comparable column (same order as <see cref="ComparableHeaders"/>), the file:// sources of the
    /// photos attached to that market data (MarketComparableImages). Empty list when none of the comparables has a photo.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> Photos { get; init; } = [];
}

/// <summary>
/// A single row in a comparison table.
/// </summary>
public sealed class ComparisonFactorRow
{
    /// <summary>
    /// Row label (factor name in Thai, e.g. "ทำเลที่ตั้ง").
    /// Null for rows where the label is unavailable.
    /// </summary>
    public string? FactorName { get; init; }

    /// <summary>MarketComparableFactors.FactorCode of the factor behind this row (null for the fixed rows).</summary>
    public string? FactorCode { get; init; }

    /// <summary>Values printed bold (the FSD prints ประเภททรัพย์สิน in bold).</summary>
    public bool Bold { get; init; }

    /// <summary>The subject's value under each <see cref="ComparisonTable.SubjectHeaders"/> entry; null = "-".</summary>
    public IReadOnlyList<string?> SubjectValues { get; init; } = [];

    /// <summary>
    /// Cell values — one per comparable, in the same order as
    /// <see cref="ComparisonTable.ComparableHeaders"/>.
    /// Individual cells may be <see langword="null"/> when no data exists.
    /// </summary>
    public IReadOnlyList<string?> Values { get; init; } = [];
}
