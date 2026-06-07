namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "วิธีต้นทุน (เครื่องจักร)" (Cost Approach – Machinery) section — FSD §2.1.2.11.
///
/// Represents one rendered block in the external appraisal report for a MachineryCost
/// pricing method: a narrative paragraph explaining the FMV formula followed by a
/// per-machine calculation grid.
///
/// All scalar properties are nullable so that partial data renders as a blank rather
/// than throwing. The section is absent from the report when
/// <see cref="CostMachineSectionLoader.LoadAsync"/> returns <see langword="null"/>
/// (no MachineryCost method exists, or the method has no MachineCostItems).
/// </summary>
public sealed class CostMachineSection
{
    /// <summary>
    /// Per-machine calculation rows, one per MachineCostItem ordered by DisplaySequence.
    /// Empty list when no items exist.
    /// </summary>
    public IReadOnlyList<MachineCostRow> Rows { get; init; } = [];

    /// <summary>
    /// รวมต้นทุนทดแทนใหม่ (บาท) — SUM(MachineCostItems.RcnReplacementCost).
    /// Null when all RCN values are null.
    /// </summary>
    public decimal? TotalRcn { get; init; }

    /// <summary>
    /// รวมมูลค่าตามสภาพ (บาท) — SUM(MachineCostItems.FairMarketValue).
    /// Null when all FMV values are null.
    /// </summary>
    public decimal? TotalFmv { get; init; }
}

/// <summary>
/// One row in the Cost Approach – Machinery calculation grid.
///
/// Primary source: <c>appraisal.MachineCostItems</c> joined to
/// <c>appraisal.MachineryAppraisalDetails</c> (via AppraisalPropertyId).
/// </summary>
public sealed class MachineCostRow
{
    // ── Sequence ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// ลำดับ — 1-based row number within the section.
    /// Derived from MachineCostItems.DisplaySequence ordering.
    /// </summary>
    public int Sequence { get; init; }

    // ── Machine identification (from MachineryAppraisalDetails) ──────────────────

    /// <summary>
    /// รายละเอียดเครื่องจักร — composed as "{MachineName} {Brand} {Model}".
    /// Sources: MachineryAppraisalDetails.MachineName (nvarchar 200),
    ///          MachineryAppraisalDetails.Brand (nvarchar 100),
    ///          MachineryAppraisalDetails.Model (nvarchar 100).
    /// Null when the MachineryAppraisalDetail row is absent.
    /// </summary>
    public string? MachineDetail { get; init; }

    /// <summary>
    /// เลขทะเบียน — Source: MachineryAppraisalDetails.RegistrationNumber (nvarchar 50).
    /// </summary>
    public string? RegistrationNumber { get; init; }

    /// <summary>
    /// ประเทศ — No Country column exists on MachineryAppraisalDetails.
    /// // no source — always null until schema adds a Country column.
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// การใช้งาน — Source: MachineryAppraisalDetails.ConditionUse (nvarchar 100).
    /// </summary>
    public string? ConditionUse { get; init; }

    // ── Age / lifespan (from both tables) ────────────────────────────────────────

    /// <summary>
    /// ปีที่ผลิต — Source: MachineryAppraisalDetails.YearOfManufacture (int?).
    /// </summary>
    public int? YearOfUse { get; init; }

    /// <summary>
    /// อายุการใช้งาน N (ปี) — Source: MachineCostItems.LifeSpanYears (decimal(5,1)).
    /// </summary>
    public decimal? LifeSpanN { get; init; }

    /// <summary>
    /// อายุจริง n — Source: MachineryAppraisalDetails.MachineAge (decimal?).
    /// Represents actual elapsed age in years as entered by the appraiser.
    /// </summary>
    public decimal? AgeN { get; init; }

    /// <summary>
    /// อายุคงเหลือ R = N - n — No stored column; not computed here.
    /// // no source — deferred; derive in template if both LifeSpanN and AgeN are present.
    /// </summary>
    public decimal? RemainingR { get; init; }

    // ── Depreciation factors (from MachineCostItems) ─────────────────────────────

    /// <summary>
    /// ปัจจัยทางสภาพ C — Source: MachineCostItems.ConditionFactor (decimal(5,2), NOT NULL).
    /// Stored as a decimal (e.g. 0.85 = 85%).
    /// </summary>
    public decimal? ConditionFactorC { get; init; }

    /// <summary>
    /// การเสื่อมราคาทางหน้าที่ F — Source: MachineCostItems.FunctionalObsolescence (decimal(5,2), NOT NULL).
    /// Stored as a decimal factor (e.g. 1.00 = no functional obsolescence).
    /// </summary>
    public decimal? FunctionalF { get; init; }

    /// <summary>
    /// การเสื่อมราคาทางเศรษฐกิจ E — Source: MachineCostItems.EconomicObsolescence (decimal(5,2), NOT NULL).
    /// Stored as a decimal factor (e.g. 1.00 = no economic obsolescence).
    /// </summary>
    public decimal? EconomicE { get; init; }

    // ── Valuation outputs (from MachineCostItems) ─────────────────────────────────

    /// <summary>
    /// ต้นทุนทดแทนใหม่ RCN (บาท) — Source: MachineCostItems.RcnReplacementCost (decimal(18,2)).
    /// </summary>
    public decimal? Rcn { get; init; }

    /// <summary>
    /// มูลค่าตามสภาพ FMV (บาท) — Source: MachineCostItems.FairMarketValue (decimal(18,2)).
    /// </summary>
    public decimal? Fmv { get; init; }

    // ── Market demand flag (from MachineCostItems) ────────────────────────────────

    /// <summary>
    /// สภาพความต้องการของตลาด — Source: MachineCostItems.MarketDemandAvailable (bit).
    /// Stored as bool on the item; surfaced as Thai display text by the loader
    /// ("มี" / "ไม่มี" / null when the flag is false).
    ///
    /// Note: MachineryAppraisalSummaries.MarketDemand (nvarchar 4000) is an appraisal-level
    /// narrative available on MachineSection, not on a per-item basis.
    /// </summary>
    public string? MarketDemand { get; init; }
}
