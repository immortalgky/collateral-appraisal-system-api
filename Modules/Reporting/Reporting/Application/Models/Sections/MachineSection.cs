namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "รายละเอียดเครื่องจักร" (Machine Details) section — FSD §2.1.2.7.
///
/// Covers two visual blocks on the printed form:
///   1. Summary header — appraisal-level counts and condition ratings from
///      <c>appraisal.MachineryAppraisalSummaries</c>.
///   2. Per-machine grid — one row per registered machine from
///      <c>appraisal.MachineryAppraisalDetails</c>.
///
/// All scalar properties are nullable so that partial data renders as a blank
/// line rather than throwing. The section is absent from the report when
/// <see cref="MachineSectionLoader.LoadAsync"/> returns <see langword="null"/>
/// (no machinery property / summary exists for the appraisal).
/// </summary>
public sealed class MachineSection
{
    // ── Summary header — appraisal.MachineryAppraisalSummaries ───────────────────

    /// <summary>
    /// จำนวนที่สำรวจ — Source: MachineryAppraisalSummaries.SurveyedNumber (int?).
    /// </summary>
    public int? SurveyedCount { get; init; }

    /// <summary>
    /// จำนวนที่ประเมิน — Source: MachineryAppraisalSummaries.AppraisalNumber (int?).
    /// The entity property is named <c>AppraisalNumber</c>; semantically it is the
    /// count of machines evaluated (not the appraisal book number).
    /// </summary>
    public int? EvaluatedCount { get; init; }

    /// <summary>
    /// จำนวนที่ติดตั้งและใช้งาน — Source: MachineryAppraisalSummaries.InstalledAndUseCount (int?).
    /// </summary>
    public int? InstalledInUseCount { get; init; }

    /// <summary>
    /// จำนวนซาก — Source: MachineryAppraisalSummaries.AppraisalScrapCount (int?).
    /// </summary>
    public int? WreckageCount { get; init; }

    /// <summary>
    /// จำนวนที่ยังไม่ติดตั้ง — Source: MachineryAppraisalSummaries.NotInstalledCount (int?).
    /// </summary>
    public int? NotInstalledCount { get; init; }

    /// <summary>
    /// สภาพการบำรุงรักษา — Source: MachineryAppraisalSummaries.Maintenance (nvarchar 500).
    /// </summary>
    public string? MaintenanceCondition { get; init; }

    /// <summary>
    /// สภาพภายนอก — Source: MachineryAppraisalSummaries.Exterior (nvarchar 500).
    /// </summary>
    public string? ExteriorCondition { get; init; }

    /// <summary>
    /// ประสิทธิภาพ — Source: MachineryAppraisalSummaries.Performance (nvarchar 500).
    /// </summary>
    public string? Efficiency { get; init; }

    /// <summary>
    /// สภาพความต้องการของตลาด — Source: MachineryAppraisalSummaries.MarketDemand (nvarchar 4000).
    /// </summary>
    public string? MarketDemand { get; init; }

    /// <summary>
    /// กรรมสิทธิ์เครื่องจักร / ชื่อเจ้าของ — Source: MachineryAppraisalSummaries.Owner (nvarchar 500).
    /// Proprietor (juristic registrant) is a separate field on the summary entity but is not
    /// surfaced here; Owner is the display-facing ownership name per FSD §2.1.2.7.
    /// </summary>
    public string? OwnerName { get; init; }

    /// <summary>
    /// ที่ตั้งเครื่องจักร — Source: MachineryAppraisalSummaries.MachineAddress (nvarchar 1000).
    /// Free-text address; no structured address VO on this entity.
    /// </summary>
    public string? MachineLocation { get; init; }

    /// <summary>
    /// ภาระผูกพัน — Source: MachineryAppraisalSummaries.Obligation (nvarchar 2000).
    /// </summary>
    public string? Obligation { get; init; }

    /// <summary>
    /// หมายเหตุ / อื่นๆ — Source: MachineryAppraisalSummaries.Other (nvarchar 4000).
    /// </summary>
    public string? Other { get; init; }

    /// <summary>
    /// รายละเอียดหลักประกัน (narrative) — no dedicated source column on
    /// MachineryAppraisalSummaries; deferred. // no source
    /// </summary>
    public string? CollateralDetailNarrative { get; init; }

    // ── Per-machine rows — appraisal.MachineryAppraisalDetails ───────────────────

    /// <summary>
    /// Rows for the per-machine detail table.
    /// Ordered by (PropertyGroup, SequenceInGroup) via the loader query.
    /// Empty when no detail rows exist (section summary may still render).
    /// </summary>
    public IReadOnlyList<MachineRow> Machines { get; init; } = [];
}

/// <summary>
/// One row in the per-machine detail table.
///
/// Source table: <c>appraisal.MachineryAppraisalDetails</c> joined via
/// <c>appraisal.AppraisalProperties</c> and <c>appraisal.PropertyGroupItems</c>.
/// All columns use their default EF Core column names (no <c>HasColumnName</c>
/// override in <c>MachineryAppraisalDetailConfiguration</c>).
/// </summary>
public sealed class MachineRow
{
    /// <summary>ลำดับ — 1-based row number within the section.</summary>
    public int Sequence { get; init; }

    /// <summary>จำนวน — Source: MachineryAppraisalDetails.Quantity (int?).</summary>
    public int? Quantity { get; init; }

    /// <summary>ชื่อเครื่องจักร — Source: MachineryAppraisalDetails.MachineName (nvarchar 200).</summary>
    public string? MachineName { get; init; }

    /// <summary>เลขทะเบียน — Source: MachineryAppraisalDetails.RegistrationNumber (nvarchar 50).</summary>
    public string? RegistrationNumber { get; init; }

    /// <summary>ยี่ห้อ — Source: MachineryAppraisalDetails.Brand (nvarchar 100).</summary>
    public string? Brand { get; init; }

    /// <summary>รุ่น — Source: MachineryAppraisalDetails.Model (nvarchar 100).</summary>
    public string? Model { get; init; }

    /// <summary>ซีรีส์ — Source: MachineryAppraisalDetails.Series (nvarchar 200).</summary>
    public string? Series { get; init; }

    /// <summary>ผู้ผลิต — Source: MachineryAppraisalDetails.Manufacturer (nvarchar 100).</summary>
    public string? Manufacturer { get; init; }

    /// <summary>ปีที่ผลิต — Source: MachineryAppraisalDetails.YearOfManufacture (int?).</summary>
    public int? YearOfManufacture { get; init; }

    /// <summary>อายุเครื่องจักร — Source: MachineryAppraisalDetails.MachineAge (decimal?).</summary>
    public decimal? MachineAge { get; init; }

    /// <summary>แบบ — Source: MachineryAppraisalDetails.Series (nvarchar 200).</summary>
    public string? Type { get; init; }

    /// <summary>หมายเลขเครื่อง — Source: MachineryAppraisalDetails.SerialNo (nvarchar).</summary>
    public string? SerialNo { get; init; }

    /// <summary>ตำแหน่งที่ตั้ง — Source: MachineryAppraisalDetails.Location (nvarchar).</summary>
    public string? Location { get; init; }

    /// <summary>ขนาดเครื่อง — Source: MachineryAppraisalDetails.MachineDimensions (nvarchar).</summary>
    public string? MachineDimensions { get; init; }

    /// <summary>พลังงานที่ใช้ — Source: MachineryAppraisalDetails.EnergyUse (nvarchar).</summary>
    public string? EnergyUse { get; init; }

    /// <summary>ใช้ในการ — Source: MachineryAppraisalDetails.UsagePurpose (nvarchar).</summary>
    public string? UsagePurpose { get; init; }

    /// <summary>ขนาดความสามารถ — Source: MachineryAppraisalDetails.Capacity (nvarchar 100).</summary>
    public string? Capacity { get; init; }

    /// <summary>ส่วนประกอบของเครื่องจักร — Source: MachineryAppraisalDetails.MachineParts (nvarchar).</summary>
    public string? MachineParts { get; init; }

    /// <summary>อื่นๆ — Source: MachineryAppraisalDetails.Other (nvarchar).</summary>
    public string? Other { get; init; }

    /// <summary>ความเห็นผู้ประเมิน — Source: MachineryAppraisalDetails.AppraiserOpinion (nvarchar).</summary>
    public string? AppraiserOpinion { get; init; }

    /// <summary>
    /// การใช้งาน — Source: MachineryAppraisalDetails.ConditionUse (nvarchar 100).
    /// </summary>
    public string? ConditionUse { get; init; }

    /// <summary>
    /// สภาพเครื่องจักร — Source: MachineryAppraisalDetails.MachineCondition (nvarchar 100).
    /// </summary>
    public string? MachineCondition { get; init; }

    /// <summary>
    /// มูลค่าทดแทน — Source: MachineryAppraisalDetails.ReplacementValue (decimal 18,2).
    /// </summary>
    public decimal? ReplacementValue { get; init; }

    /// <summary>
    /// มูลค่าตามสภาพ — Source: MachineryAppraisalDetails.ConditionValue (decimal 18,2).
    /// </summary>
    public decimal? ConditionValue { get; init; }

    /// <summary>หมายเหตุ — Source: MachineryAppraisalDetails.Remark (nvarchar 4000).</summary>
    public string? Remark { get; init; }
}
