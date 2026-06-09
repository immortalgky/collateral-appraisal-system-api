using Reporting.Application.Models.Sections;

namespace Reporting.Application.Models;

/// <summary>
/// ViewModel for the "ใบสรุปรายงานการประเมิน – ที่ดินและสิ่งปลูกสร้าง"
/// (Appraisal Summary Report – Land and Building), FSD §2.1.3.1.
///
/// All reference types are nullable so partial data renders as blank rather than
/// throwing.  Collections default to empty.
///
/// Section props (LandSection … AppendixSection, AttachmentsBySlot) are only
/// populated by the composite internal-report providers (FSD §2.1.6 / §2.1.7).
/// Standalone summary providers leave them null/empty, so existing reports are
/// completely unaffected.
/// </summary>
public sealed class AppraisalSummaryModel
{
    // ── Header ───────────────────────────────────────────────────────────────────

    /// <summary>Field 1 — Appraisal book number (e.g. "APP-2567-00042").</summary>
    public string? AppraisalBookNumber { get; init; }

    /// <summary>Field 2 — Inspection/appointment date (used as "appraisal date").</summary>
    public DateTime? AppraisalDate { get; init; }

    /// <summary>Field 3 — Customer names joined with " และ ".</summary>
    public string? CustomerName { get; init; }

    /// <summary>
    /// Field 4 — AO (Account Officer) name and department.
    /// No verified source column in the current schema — deferred (null).
    /// </summary>
    public string? AoName { get; init; }

    /// <summary>Field 5 — Appraisal purpose code/description.</summary>
    public string? AppraisalPurpose { get; init; }

    /// <summary>Field 6 — Property type (first property's PropertyType code).</summary>
    public string? PropertyType { get; init; }

    /// <summary>Field 7 — Full collateral address built via ThaiAddressFormatter.</summary>
    public string? CollateralAddress { get; init; }

    /// <summary>Field 8 — Administrative sub-district (ตำบล/แขวง).</summary>
    public string? AdministrativeDistrict { get; init; }

    /// <summary>Field 9 — Land Office name.</summary>
    public string? LandOffice { get; init; }

    /// <summary>
    /// Field 10 — Old appraisal value.
    /// Only populated when AppraisalType is ReAppraisal; null otherwise.
    /// </summary>
    public decimal? OldAppraisalValue { get; init; }

    /// <summary>
    /// Field 11 — Appraiser line.
    /// Internal: literal bank name.  External: company name.
    /// </summary>
    public string? Appraiser { get; init; }

    /// <summary>
    /// Field 12 — Loan / facility limit.
    /// Only populated when Purpose implies a new loan; null otherwise.
    /// </summary>
    public decimal? LoanValue { get; init; }

    // ── Collateral detail rows ────────────────────────────────────────────────────

    /// <summary>Fields 13–19 — One row per property group.</summary>
    public IReadOnlyList<SummaryGroupRow> Groups { get; init; } = [];

    // ── Totals ───────────────────────────────────────────────────────────────────

    /// <summary>Field 20 — Total appraisal value (sum of group appraisal values).</summary>
    public decimal? TotalAppraisalValue { get; init; }

    /// <summary>
    /// Field 21 — Building coverage / insurance amount.
    /// Source: ValuationAnalyses.InsuranceValue (the single InsuranceValue stored at
    /// the overall appraisal level, not per-group).
    /// </summary>
    public decimal? BuildingCoverageAmount { get; init; }

    /// <summary>Field 22 — Forced sale value (70% of total appraisal value by convention).</summary>
    public decimal? ForcedSaleValue { get; init; }

    /// <summary>Field 18 — Overall condition (from AppraisalDecisions.Condition).</summary>
    public string? Condition { get; init; }

    /// <summary>Field 19 — Overall remark (from AppraisalDecisions.Remark).</summary>
    public string? Remark { get; init; }

    // ── Property attributes ───────────────────────────────────────────────────────

    /// <summary>Field 23 — Land owner name.</summary>
    public string? LandOwner { get; init; }

    /// <summary>Field 24 — Entry/exit rights description.</summary>
    public string? EntryExitRights { get; init; }

    /// <summary>Field 25 — Building owner name.</summary>
    public string? BuildingOwner { get; init; }

    /// <summary>Field 26 — Land condition / obligation description.</summary>
    public string? LandCondition { get; init; }

    /// <summary>Field 27 — Obligation (encumbrance) details.</summary>
    public string? Obligation { get; init; }

    /// <summary>Field 28 — Urban planning / city plan colour (UrbanPlanningType).</summary>
    public string? CityPlan { get; init; }

    /// <summary>
    /// Field 29 — GPS coordinates as "Lat, Lon" string.
    /// Built from LandAppraisalDetails.Latitude / Longitude value-object columns.
    /// </summary>
    public string? Gps { get; init; }

    /// <summary>
    /// Field 30 — Government assessed value.
    /// Source: GovernmentPrice from LandTitles (first verified title).
    /// </summary>
    public decimal? GovernmentAssessedValue { get; init; }

    /// <summary>Field 31 — Utilization / current use description.</summary>
    public string? Utilization { get; init; }

    // ── Machine-variant attributes (§2.1.3.3; null for Land/Condo) ─────────────────

    /// <summary>Machine type (ประเภทเครื่องจักร).</summary>
    public string? MachineType { get; init; }

    /// <summary>Market demand / saleability conditions (สภาพความต้องการของตลาด).</summary>
    public string? MarketDemandConditions { get; init; }

    // ── Construction-variant (§2.1.4 ตรวจงวดก่อสร้าง; null for other variants) ──────

    /// <summary>อ้างอิงรายงานการประเมิน (refer to a prior appraisal book) — checkbox.</summary>
    public bool IsReferAppraisalBook { get; init; }
    /// <summary>Refer appraisal book number (prior appraisal's AppraisalNumber).</summary>
    public string? ReferAppraisalBookNumber { get; init; }
    /// <summary>อ้างอิงรายงานการตรวจงานก่อสร้าง — checkbox.</summary>
    public bool IsReferConstructionBook { get; init; }
    /// <summary>Refer construction-inspection book number.</summary>
    public string? ReferConstructionBookNumber { get; init; }
    /// <summary>ตรวจครั้งที่ (inspection round) — no stored source; deferred.</summary>
    public string? InspectionRound { get; init; }
    /// <summary>งวดงานที่ (installment work no) — no stored source; deferred.</summary>
    public string? InstallmentNumber { get; init; }
    /// <summary>ชื่ออาคาร (building name).</summary>
    public string? BuildingName { get; init; }
    /// <summary>ราคาประเมิน เนื่องจากงานเสร็จ 100% (building value at 100%).</summary>
    public decimal? BuildingValue100 { get; init; }
    /// <summary>ราคาประเมินที่ดิน (land appraisal value).</summary>
    public decimal? LandAppraisalValue { get; init; }
    /// <summary>มูลค่าอาคาร ณ ปัจจุบัน (current building value).</summary>
    public decimal? CurrentBuildingValue { get; init; }
    /// <summary>รวมราคา ที่ดิน+อาคาร (100%).</summary>
    public decimal? TotalLandBuilding100 { get; init; }
    /// <summary>รวมราคา ที่ดิน+อาคาร ณ ปัจจุบัน.</summary>
    public decimal? TotalLandCurrentBuilding { get; init; }
    /// <summary>ผลงานก่อนหน้า/เดิม % (previous progress).</summary>
    public decimal? PreviousProgressPct { get; init; }
    /// <summary>ผลงานที่สร้างเพิ่มขึ้น % (current increment).</summary>
    public decimal? AdditionalProgressPct { get; init; }
    /// <summary>รวมผลการดำเนินงาน % (total progress).</summary>
    public decimal? TotalProgressPct { get; init; }
    /// <summary>สำเนาใบอนุญาตก่อสร้าง uploaded — checkbox (deferred default).</summary>
    public bool HasConstructionLicense { get; init; }
    /// <summary>ตารางแสดงผลงานการก่อสร้าง present — checkbox (CI document attached).</summary>
    public bool HasProgressTable { get; init; }
    /// <summary>ภาพถ่ายการก่อสร้าง present — checkbox (deferred default).</summary>
    public bool HasConstructionPhoto { get; init; }
    /// <summary>Per-building progress breakdown remark (หมายเหตุ).</summary>
    public string? ConstructionRemark { get; init; }

    // ── Block-variant (§2.1.5 Block; null for other variants) ─────────────────────

    /// <summary>ชื่อโครงการ (project name).</summary>
    public string? ProjectName { get; init; }
    /// <summary>ที่ตั้งโครงการ (project address, composed).</summary>
    public string? ProjectAddress { get; init; }
    /// <summary>เจ้าของโครงการ (developer).</summary>
    public string? Developer { get; init; }
    /// <summary>ลักษณะโครงการ (project details + house-model list, composed).</summary>
    public string? ProjectDetails { get; init; }
    /// <summary>วันที่เปิดขายโครงการ (sale launch date — display string).</summary>
    public string? ProjectSaleLaunchDate { get; init; }
    /// <summary>สาธารณูปโภค (utilities).</summary>
    public string? Utilities { get; init; }
    /// <summary>สิ่งอำนวยความสะดวก (facilities).</summary>
    public string? Facilities { get; init; }
    /// <summary>Default wording for the block appraisal-value line.</summary>
    public string? AppraisalValueWording { get; init; }
    /// <summary>Summary Table of Assessed Price – Building (LB/L projects).</summary>
    public IReadOnlyList<BlockBuildingUnitRow> BuildingUnits { get; init; } = [];
    /// <summary>Summary Table of Assessed Price – Condo (U projects).</summary>
    public IReadOnlyList<BlockCondoUnitRow> CondoUnits { get; init; } = [];

    // ── Price analysis method flags ───────────────────────────────────────────────

    /// <summary>Field 32 — WQS (Weighted Quantity Survey) method used.</summary>
    public bool IsWqs { get; init; }

    /// <summary>Field 33 — Sale Grid / Direct Comparison method used.</summary>
    public bool IsSaleGrid { get; init; }

    /// <summary>Field 34 — Cost approach method used (BuildingCost).</summary>
    public bool IsCost { get; init; }

    /// <summary>Field 35 — Income approach method used.</summary>
    public bool IsIncome { get; init; }

    /// <summary>Field 36 — Hypothesis (Land+Building or Condo) method used.</summary>
    public bool IsHypothesis { get; init; }

    /// <summary>Field 37 — Leasehold method used.</summary>
    public bool IsLeasehold { get; init; }

    /// <summary>Field 38 — Profit Rent method used.</summary>
    public bool IsProfitRent { get; init; }

    // ── Appraiser comment ─────────────────────────────────────────────────────────

    /// <summary>Field 39 — Appraiser opinion / comment (AppraiserOpinion or CommitteeOpinion).</summary>
    public string? AppraiserComment { get; init; }

    // ── Sign-off ──────────────────────────────────────────────────────────────────

    /// <summary>Field 40 — Staff appraiser full name.</summary>
    public string? AppraisalStaffName { get; init; }

    /// <summary>Field 40a — Staff appraiser position.</summary>
    public string? AppraisalStaffPosition { get; init; }

    /// <summary>
    /// Field 41 — Checker name.
    /// No dedicated checker role column in the current schema — deferred (null).
    /// </summary>
    public string? AppraisalCheckerName { get; init; }

    /// <summary>Field 41a — Checker position (deferred, null).</summary>
    public string? AppraisalCheckerPosition { get; init; }

    /// <summary>
    /// Field 41b — Verify-level name.
    /// No dedicated verify role column in the current schema — deferred (null).
    /// </summary>
    public string? AppraisalVerifyName { get; init; }

    /// <summary>Field 41c — Verify-level position (deferred, null).</summary>
    public string? AppraisalVerifyPosition { get; init; }

    // ── Committee / approver block (fields 42–51) ─────────────────────────────────

    /// <summary>
    /// Field 42 — Meeting number string (e.g. "12/2567").
    /// Only displayed when ShowMeeting = true (FacilityLimit > 30M, group 3).
    /// </summary>
    public string? MeetingNumber { get; init; }

    /// <summary>Field 43 — Meeting date.</summary>
    public DateTime? MeetingDate { get; init; }

    /// <summary>
    /// True when the committee/meeting block should be shown.
    /// Gate: FacilityLimit > 30,000,000 (application group 3) AND a review exists.
    /// </summary>
    public bool ShowMeeting { get; init; }

    /// <summary>
    /// Field 44 — Overall approval decision.
    /// Derived: VotesApprove > VotesReject among CommitteeVotes for the review.
    /// </summary>
    public bool? ApproverDecisionApproved { get; init; }

    /// <summary>Fields 45–50 — Individual approver rows.</summary>
    public IReadOnlyList<ApproverRow> Approvers { get; init; } = [];

    /// <summary>Field 51 — Committee summary comment (CommitteeOpinion).</summary>
    public string? ApproverSummaryComment { get; init; }

    // ── Composite section props (§2.1.6 Internal-Construction / §2.1.7 Internal-Block) ──
    // Set only by InternalConstructionReportProvider / InternalBlockReportProvider.
    // Standalone summary providers leave all of these null/empty (zero behaviour change).

    /// <summary>Land details section (§2.1.2.4). Null when not included in this report.</summary>
    public LandSection? LandSection { get; set; }

    /// <summary>Building details section (§2.1.2.5). Null when not included in this report.</summary>
    public BuildingSection? BuildingSection { get; set; }

    /// <summary>Construction progress section (§2.1.2.6). Null when not included in this report.</summary>
    public ConstructionSection? ConstructionSection { get; set; }

    /// <summary>Market comparison section (§2.1.2.8). Null when not included in this report.</summary>
    public ComparisonSection? ComparisonSection { get; set; }

    /// <summary>WQS price analysis section (§2.1.2.9). Null when not included in this report.</summary>
    public WqsSection? WqsSection { get; set; }

    /// <summary>Sale grid / direct comparison section (§2.1.2.10). Null when not included.</summary>
    public SaleGridSection? SaleGridSection { get; set; }

    /// <summary>Appendix image section (§2.1.2.12+). Null when no image entries exist.</summary>
    public AppendixSection? AppendixSection { get; set; }

    /// <summary>
    /// PDF document IDs keyed by slot name (e.g. "appendix").
    /// Merged at the corresponding &lt;!-- SLOT: name --&gt; marker by PdfSharpAssembler.
    /// Defaults to empty; populated by internal-report providers via AppendixSectionLoader.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Guid>> AttachmentsBySlot { get; set; }
        = new Dictionary<string, IReadOnlyList<Guid>>();
}

/// <summary>One collateral group row in the per-group valuation table (fields 13–19).</summary>
public sealed class SummaryGroupRow
{
    /// <summary>Group number (1-based sequence).</summary>
    public int GroupNumber { get; init; }

    /// <summary>Group name / description.</summary>
    public string? GroupName { get; init; }

    /// <summary>Property type code for this group.</summary>
    public string? PropertyType { get; init; }

    /// <summary>Collateral details (title number / unit / address snippet).</summary>
    public string? CollateralDetails { get; init; }

    /// <summary>Land area in rai-ngan-wa format, or unit count for buildings.</summary>
    public string? AreaOrUnit { get; init; }

    /// <summary>Price per unit (from GroupValuations.ValuePerUnit).</summary>
    public decimal? PricePerAreaOrUnit { get; init; }

    /// <summary>Group appraisal value (from GroupValuations.AppraisedValue).</summary>
    public decimal? AppraisalValue { get; init; }

    /// <summary>Condition text for this group.</summary>
    public string? Condition { get; init; }

    /// <summary>Remark text for this group.</summary>
    public string? Remark { get; init; }
}

/// <summary>One committee approver row (fields 45–50).</summary>
public sealed class ApproverRow
{
    public string? Name { get; init; }
    public string? Position { get; init; }
    public string? Comment { get; init; }

    /// <summary>This member's own vote — true=Approve, false=Reject, null=other/abstain.
    /// Drives the per-row อนุมัติ/ไม่อนุมัติ checkbox in the committee table.</summary>
    public bool? Approved { get; init; }
}

/// <summary>One row in the Block "Summary Table of Assessed Price – Building" (§2.1.5 fields 39–49).</summary>
public sealed class BlockBuildingUnitRow
{
    public int Sequence { get; init; }
    public string? PlotNumber { get; init; }
    public string? HouseNumber { get; init; }
    public string? ModelType { get; init; }
    public decimal? LandArea { get; init; }
    public decimal? UsableArea { get; init; }
    public decimal? SellingPrice { get; init; }
    public decimal? AppraisalValue { get; init; }
    public decimal? ForcedSaleValue { get; init; }
    public decimal? CoverageAmount { get; init; }
}

/// <summary>One row in the Block "Summary Table of Assessed Price – Condo" (§2.1.5 fields 50–61).</summary>
public sealed class BlockCondoUnitRow
{
    public int Sequence { get; init; }
    public int? Floor { get; init; }
    public string? TowerName { get; init; }
    public string? RoomNumber { get; init; }
    public string? ModelType { get; init; }
    public decimal? UsableArea { get; init; }
    public decimal? AppraisalValue { get; init; }
    public decimal? PricePerSqm { get; init; }
    public decimal? ForcedSaleValue { get; init; }
    public decimal? CoverageAmount { get; init; }
}
