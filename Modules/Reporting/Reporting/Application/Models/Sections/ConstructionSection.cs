namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "ตารางรายละเอียดความคืบหน้างานก่อสร้าง"
/// (Construction Progress Details table) — FSD §2.1.2.6.
///
/// One <see cref="ConstructionBuilding"/> per AppraisalProperty that has an
/// <c>IsFullDetail = true</c> ConstructionInspection row.  The section is
/// absent from the report when <see cref="ConstructionSectionLoader.LoadAsync"/>
/// returns <see langword="null"/> (appraisal has no full-detail CI rows).
///
/// All numeric properties are nullable so partial data renders as blank.
/// </summary>
public sealed class ConstructionSection
{
    /// <summary>
    /// One entry per building property that carries a full-detail CI record.
    /// Ordered by AppraisalProperty.SequenceNumber.
    /// </summary>
    public IReadOnlyList<ConstructionBuilding> Buildings { get; init; } = [];
}

/// <summary>
/// Per-building breakdown of construction progress work groups and items.
/// </summary>
public sealed class ConstructionBuilding
{
    /// <summary>
    /// ชื่ออาคาร — source: appraisal.BuildingAppraisalDetails.PropertyName for the
    /// AppraisalPropertyId that owns this CI row.
    /// </summary>
    public string? BuildingName { get; init; }

    /// <summary>
    /// Work groups for this building, ordered by parameter.ConstructionWorkGroups.DisplayOrder.
    /// </summary>
    public IReadOnlyList<ConstructionWorkGroupRow> Groups { get; init; } = [];

    // ── รวมมูลค่าอาคาร (building totals) ──────────────────────────────────────

    /// <summary>SUM(ConstructionWorkDetails.ConstructionValue) for this building.</summary>
    public decimal? TotalValue { get; init; }

    /// <summary>SUM(ProportionPct) — should equal 100 when fully specified.</summary>
    public decimal? TotalProportionPct { get; init; }

    /// <summary>SUM(PreviousProgressPct × ProportionPct / 100) across all items.</summary>
    public decimal? TotalPreviousPct { get; init; }

    /// <summary>SUM(CurrentProgressPct × ProportionPct / 100) across all items.</summary>
    public decimal? TotalCurrentPct { get; init; }

    /// <summary>SUM(PreviousPropertyValue) across all items.</summary>
    public decimal? TotalPreviousValue { get; init; }

    /// <summary>SUM(CurrentPropertyValue) across all items.</summary>
    public decimal? TotalCurrentValue { get; init; }
}

/// <summary>
/// A single construction work group (e.g. "งานโครงสร้าง", "งานสถาปัตยกรรม")
/// with its constituent work items and subtotals.
/// </summary>
public sealed class ConstructionWorkGroupRow
{
    /// <summary>
    /// Thai group name — source: parameter.ConstructionWorkGroups.NameTh
    /// for the ConstructionWorkGroupId carried on each ConstructionWorkDetail row.
    /// </summary>
    public string? GroupName { get; init; }

    /// <summary>Work items within this group, ordered by DisplayOrder.</summary>
    public IReadOnlyList<ConstructionWorkItemRow> Items { get; init; } = [];

    // ── Group subtotals ───────────────────────────────────────────────────────

    /// <summary>SUM(ConstructionValue) for items in this group.</summary>
    public decimal? Value { get; init; }

    /// <summary>SUM(ProportionPct) for items in this group.</summary>
    public decimal? ProportionPct { get; init; }

    /// <summary>SUM(PreviousProgressPct × ProportionPct / 100) for items in this group.</summary>
    public decimal? PreviousPct { get; init; }

    /// <summary>SUM(CurrentProgressPct × ProportionPct / 100) for items in this group.</summary>
    public decimal? CurrentPct { get; init; }

    /// <summary>SUM(PreviousPropertyValue) for items in this group.</summary>
    public decimal? PreviousValue { get; init; }

    /// <summary>SUM(CurrentPropertyValue) for items in this group.</summary>
    public decimal? CurrentValue { get; init; }
}

/// <summary>
/// One work item detail row within a construction work group.
/// Maps 1:1 to a appraisal.ConstructionWorkDetails row.
/// </summary>
public sealed class ConstructionWorkItemRow
{
    /// <summary>
    /// รายการ — source: ConstructionWorkDetails.WorkItemName.
    /// </summary>
    public string? ItemName { get; init; }

    /// <summary>
    /// มูลค่าก่อสร้าง (บาท) — source: ConstructionWorkDetails.ConstructionValue.
    /// Computed as TotalValue × (ProportionPct / 100).
    /// </summary>
    public decimal? Value { get; init; }

    /// <summary>
    /// สัดส่วน (%) — source: ConstructionWorkDetails.ProportionPct.
    /// </summary>
    public decimal? ProportionPct { get; init; }

    /// <summary>
    /// ครั้งก่อนหน้า (%) — source: ConstructionWorkDetails.PreviousProgressPct.
    /// </summary>
    public decimal? PreviousPct { get; init; }

    /// <summary>
    /// ปัจจุบัน (%) — source: ConstructionWorkDetails.CurrentProgressPct.
    /// </summary>
    public decimal? CurrentPct { get; init; }

    /// <summary>
    /// ครั้งก่อนหน้า (บาท) — source: ConstructionWorkDetails.PreviousPropertyValue.
    /// Computed as ConstructionValue × (PreviousProgressPct / 100).
    /// </summary>
    public decimal? PreviousValue { get; init; }

    /// <summary>
    /// ปัจจุบัน (บาท) — source: ConstructionWorkDetails.CurrentPropertyValue.
    /// Computed as ConstructionValue × (CurrentProgressPct / 100).
    /// </summary>
    public decimal? CurrentValue { get; init; }
}
