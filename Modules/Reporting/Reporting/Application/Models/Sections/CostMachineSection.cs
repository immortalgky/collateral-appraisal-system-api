namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "วิธีต้นทุน (เครื่องจักร)" (Cost Approach – Machinery) section — FSD §2.1.2.11.
///
/// Layout (per FSD): a full-page methodology cover (rendered statically in the template),
/// followed by ONE calculation table per machine <b>property group</b>. Each table is headed
/// by its group name and carries its own totals row. Machines that belong to no property
/// group fall into a single ungrouped table (GroupNumber 0, no header).
///
/// The section is absent from the report when
/// <see cref="CostMachineSectionLoader.LoadAsync"/> returns <see langword="null"/>
/// (no MachineryCost method exists, or the method has no MachineCostItems).
/// </summary>
public sealed class CostMachineSection
{
    // ── Property group routing ────────────────────────────────────────────────────

    /// <summary>
    /// PropertyGroups.GroupNumber — 0 for the shared machinery section.
    /// CostMachine is not split per property group at the outer section level;
    /// internal splitting uses MachineCostGroup.GroupNumber instead.
    /// </summary>
    public int GroupNumber { get; init; }

    /// <summary>PropertyGroups.GroupName — null for the shared machinery section.</summary>
    public string? GroupName { get; init; }

    /// <summary>
    /// One block per machine property group, ordered by GroupNumber ascending.
    /// Each group renders as a separate table with its own header and totals.
    /// </summary>
    public IReadOnlyList<MachineCostGroup> Groups { get; init; } = [];
}

/// <summary>
/// One machine property group = one rendered cost table.
/// Grouping source: <c>appraisal.PropertyGroups</c> / <c>appraisal.PropertyGroupItems</c>.
/// </summary>
public sealed class MachineCostGroup
{
    /// <summary>กลุ่มที่ — PropertyGroups.GroupNumber (0 = ungrouped fallback, renders without a header).</summary>
    public int GroupNumber { get; init; }

    /// <summary>ชื่อกลุ่ม — PropertyGroups.GroupName. Used as the table header.</summary>
    public string? GroupName { get; init; }

    /// <summary>Per-machine calculation rows, ordered by DisplaySequence within the group.</summary>
    public IReadOnlyList<MachineCostRow> Rows { get; init; } = [];

    /// <summary>
    /// จำนวน X เครื่อง — number of machine rows listed in this group (the totals-row narrative).
    /// </summary>
    public int MachineCount { get; init; }

    /// <summary>
    /// สำรวจพบ Y เครื่อง — SUM(Quantity) across the group's rows (machines actually found on survey).
    /// Also the value printed in the totals row's จำนวน column.
    /// </summary>
    public int SurveyedCount { get; init; }

    /// <summary>รวมมูลค่าทดแทน RCN (บาท) — SUM(Rcn) for the group. Null when all RCN values are null.</summary>
    public decimal? TotalRcn { get; init; }

    /// <summary>รวมมูลค่าตามสภาพ FMV (บาท) — SUM(Fmv) for the group. Null when all FMV values are null.</summary>
    public decimal? TotalFmv { get; init; }
}

/// <summary>
/// One row in a Cost Approach – Machinery calculation table.
///
/// Primary source: <c>appraisal.MachineCostItems</c> joined to
/// <c>appraisal.MachineryAppraisalDetails</c> (via AppraisalPropertyId).
/// Formula (FSD): FMV = RCN × P × F × E, where P = (1 − n/N) × C and R = N − n.
/// RCN and FMV are stored (FMV computed client-side on save); P and R are derived here for display.
/// </summary>
public sealed class MachineCostRow
{
    /// <summary>ลำดับที่ — 1-based row number within the group.</summary>
    public int Sequence { get; init; }

    /// <summary>จำนวน — Source: MachineryAppraisalDetails.Quantity (int?).</summary>
    public int? Quantity { get; init; }

    /// <summary>
    /// ชื่อและรายละเอียดเครื่องจักรพร้อมอุปกรณ์ — composed as "{MachineName} {Brand} {Model}".
    /// Null when the MachineryAppraisalDetail row is absent.
    /// </summary>
    public string? MachineDetail { get; init; }

    /// <summary>หมายเลขทะเบียนเครื่องจักร — Source: MachineryAppraisalDetails.RegistrationNumber.</summary>
    public string? RegistrationNumber { get; init; }

    /// <summary>ประเทศผู้ผลิตผู้ประกอบ — Source: MachineryAppraisalDetails.Manufacturer (holds the maker/country).</summary>
    public string? ManufacturerCountry { get; init; }

    /// <summary>สภาพการใช้งาน — Source: MachineryAppraisalDetails.ConditionUse.</summary>
    public string? ConditionUse { get; init; }

    /// <summary>
    /// ปีที่เริ่มใช้งาน (พ.ศ.) — Source: MachineryAppraisalDetails.YearOfManufacture (int?).
    /// Rendered as stored (the FSD column is labelled พ.ศ. and the value is captured in Buddhist era).
    /// </summary>
    public int? YearOfUse { get; init; }

    /// <summary>N (ปี) อายุใช้งานทางกายภาพ — Source: MachineCostItems.LifeSpanYears (decimal(5,1)).</summary>
    public decimal? LifeSpanN { get; init; }

    /// <summary>n (ปี) อายุตามระยะเวลาที่ใช้งาน — Source: MachineryAppraisalDetails.MachineAge (decimal?).</summary>
    public decimal? AgeN { get; init; }

    /// <summary>R (ปี) อายุคงเหลือ — Derived: R = N − n. Null when N or n is missing.</summary>
    public decimal? RemainingR { get; init; }

    /// <summary>C ปัจจัยทางสภาพ — Source: MachineCostItems.ConditionFactor (decimal(5,2)).</summary>
    public decimal? ConditionFactorC { get; init; }

    /// <summary>P การเสื่อมราคาทางกายภาพ — Derived: P = (1 − n/N) × C. Null when N or n is missing.</summary>
    public decimal? PhysicalP { get; init; }

    /// <summary>F การเสื่อมราคาทางประโยชน์ใช้สอย — Source: MachineCostItems.FunctionalObsolescence (decimal(5,2)).</summary>
    public decimal? FunctionalF { get; init; }

    /// <summary>E การเสื่อมราคาทางเศรษฐกิจ/ปัจจัยภายนอก — Source: MachineCostItems.EconomicObsolescence (decimal(5,2)).</summary>
    public decimal? EconomicE { get; init; }

    /// <summary>มูลค่าทดแทน RCN (บาท) — Source: MachineCostItems.RcnReplacementCost (decimal(18,2)).</summary>
    public decimal? Rcn { get; init; }

    /// <summary>มูลค่าตามสภาพ FMV (บาท) — Source: MachineCostItems.FairMarketValue (decimal(18,2)).</summary>
    public decimal? Fmv { get; init; }

    /// <summary>
    /// ความต้องการตลาด ใช้งานได้/ใช้งานไม่ได้ — Source: MachineCostItems.MarketDemandAvailable (bit).
    /// Surfaced as "ใช้งานได้" / "ใช้งานไม่ได้" by the loader.
    /// </summary>
    public string? MarketDemand { get; init; }
}
