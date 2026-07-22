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

    /// <summary>
    /// Report title shown in the header band. Set by the provider so it can vary by
    /// collateral type (e.g. land-only "…ราคาที่ดิน" vs "…ราคาทรัพย์สิน").
    /// Templates fall back to their own literal when this is null.
    /// </summary>
    public string? ReportTitle { get; init; }

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

    /// <summary>Appraisal criteria / basis of value (หลักเกณฑ์การประเมิน) — fixed wording per FSD.</summary>
    public string? AppraisalCriteria { get; init; }

    /// <summary>Field 6 — Property type (header) — the property's direct type.</summary>
    public string? PropertyType { get; init; }

    /// <summary>
    /// Property type shown in the appraiser-opinion section. Auto-detected from actual
    /// contents (land+building / land / building) for the land-building summary; null for
    /// other variants (opinion falls back to <see cref="PropertyType"/>).
    /// </summary>
    public string? SummaryPropertyType { get; init; }

    /// <summary>Field 7 — Full collateral address built via ThaiAddressFormatter.</summary>
    public string? CollateralAddress { get; init; }

    /// <summary>Field 8 — Administrative sub-district (ตำบล/แขวง).</summary>
    public string? AdministrativeDistrict { get; init; }

    /// <summary>Field 9 — Land Office name.</summary>
    public string? LandOffice { get; init; }

    /// <summary>
    /// Field 10 — Old appraisal value: the prior appraisal's live appraised value, resolved
    /// through Appraisals.PrevAppraisalId. Null when there is no prior appraisal, or when the
    /// prior appraisal has no valuation yet (in which case <see cref="HasPrevAppraisal"/> is
    /// still true and the row renders with a dash).
    /// </summary>
    public decimal? OldAppraisalValue { get; init; }

    /// <summary>True when the appraisal has a PrevAppraisalId — gates the ราคาประเมินเดิม row.
    /// Deliberately independent of AppraisalType: any appraisal carrying a prior-appraisal link
    /// has a meaningful previous value to show.</summary>
    public bool HasPrevAppraisal { get; init; }

    /// <summary>True when AppraisalType is ReAppraisal. Still gates the วงเงินสินเชื่อ row
    /// (hidden for reappraisals); no longer gates ราคาประเมินเดิม — see <see cref="HasPrevAppraisal"/>.</summary>
    public bool IsReAppraisal { get; init; }

    /// <summary>
    /// Field 11 — Appraiser line.
    /// Internal: literal bank name.  External: company name.
    /// </summary>
    public string? Appraiser { get; init; }

    /// <summary>
    /// Field 12 — Loan / facility limit (the last-row amount).
    /// Labelled วงเงินสินเชื่อ by default, ขอเพิ่มวงเงิน when <see cref="IsIncreaseLimit"/>.
    /// </summary>
    public decimal? LoanValue { get; init; }

    /// <summary>
    /// True for the "increase credit limit" purpose. Drives BOTH the วงเงินสินเชื่อเดิม
    /// (existing limit) row and the ขอเพิ่มวงเงิน relabel of the loan row. Default false
    /// (space reserved but hidden) until the specific purpose + source are wired.
    /// </summary>
    public bool IsIncreaseLimit { get; init; }

    /// <summary>วงเงินสินเชื่อเดิม — existing credit limit; shown only when <see cref="IsIncreaseLimit"/>.</summary>
    public decimal? ExistingLoanValue { get; init; }

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

    /// <summary>Field 26 — Land condition / obligation description (joined fallback).</summary>
    public string? LandCondition { get; init; }

    /// <summary>Field 26 — Land condition per group (rendered one line each).</summary>
    public IReadOnlyList<string> LandConditions { get; init; } = [];

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

    /// <summary>
    /// Field 30 — Government price text (per sq.wa), grouped by same price with title-number
    /// detail when prices differ. Preferred over <see cref="GovernmentAssessedValue"/> for display.
    /// </summary>
    public string? GovernmentPriceText { get; init; }

    /// <summary>Field 31 — Utilization / current use description (joined fallback).</summary>
    public string? Utilization { get; init; }

    /// <summary>Field 31 — Utilization per group (rendered one line each).</summary>
    public IReadOnlyList<string> Utilizations { get; init; } = [];

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
    /// <summary>ตรวจครั้งที่ in the อ้างอิง row — round of the PREVIOUS inspection being referenced.</summary>
    public string? InspectionRound { get; init; }
    /// <summary>ตรวจครั้งที่ in the value block — round of THIS (current) inspection.</summary>
    public string? CurrentInspectionRound { get; init; }
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
    /// <summary>วันที่เปิดขายโครงการ — preformatted Thai partial date (year / month-year / day-month-year).</summary>
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

    /// <summary>Field 39 — Appraiser opinion / comment. Sourced uniformly from the bank's internal
    /// opinion (AppraisalDecision.InternalAppraiserOpinion): the book-verifier's opinion on the
    /// external assignment path, the internal appraiser's own opinion on the internal path.</summary>
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

    /// <summary>Latest approval-vote date — used for the sub-committee header (no meeting).</summary>
    public DateTime? ApprovalDate { get; init; }

    /// <summary>True when the appraisal status is Completed — gates the committee/approval block.</summary>
    public bool IsCompleted { get; init; }

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

    /// <summary>Optional override for the ราคาประเมิน row in the committee table (e.g. block → "ตามแนบ").
    /// When null the numeric TotalAppraisalValue is shown.</summary>
    public string? ApprovalValueText { get; init; }

    /// <summary>Optional override for the รายการทรัพย์สิน row in the committee table (e.g. block →
    /// ชื่อโครงการ + ที่ตั้งโครงการ). When null CollateralAddress is shown.</summary>
    public string? ApprovalPropertyText { get; init; }

    // ── Unified appraisal-book discriminators (set by AppraisalBookDataProvider) ──────
    // For the standalone summary forms these stay default (IsExternal=false, BodyType=null)
    // and are simply ignored.

    /// <summary>True when the appraisal was done by an external company (drives external cover/letter).</summary>
    public bool IsExternal { get; set; }

    /// <summary>Internal body variant: "standard" | "construction" | "block". Null for external.</summary>
    public string? BodyType { get; set; }

    // ── External cover / company-letter fields (set only on the external book branch) ──
    // Populated by ExternalBookBuilder; null for every internal/standalone form.

    /// <summary>External company name (auth.Companies.Name).</summary>
    public string? CompanyName { get; set; }
    /// <summary>External company composed address.</summary>
    public string? CompanyAddress { get; set; }
    /// <summary>External company telephone.</summary>
    public string? CompanyTel { get; set; }
    /// <summary>Literal bank name shown as the "เสนอ" recipient on the external cover/letter.</summary>
    public string? BankName { get; set; }
    /// <summary>Report/verification date used on the company letter.</summary>
    public DateTime? VerifyDate { get; set; }
    /// <summary>Letter subject line (fixed wording).</summary>
    public string? Subject { get; set; }
    /// <summary>Property-type summary string (e.g. "บ้านเดี่ยว จำนวน 1 หลัง เนื้อที่ …").</summary>
    public string? PropertyTypeSummary { get; set; }
    /// <summary>Composed collateral location (ThaiAddressFormatter) for the external cover/letter.</summary>
    public string? CollateralLocation { get; set; }
    /// <summary>Comma-joined land title deed numbers.</summary>
    public string? TitleDeedNumbers { get; set; }
    /// <summary>Total count of land title deeds.</summary>
    public int? TotalTitleDeeds { get; set; }
    /// <summary>Land area formatted "{rai} - {ngan} - {wa} ไร่ หรือ {totalSqWa} ตารางวา".</summary>
    public string? LandAreaText { get; set; }
    /// <summary>Building details text ("{type} {floors} ชั้น แบบ {model}").</summary>
    public string? BuildingDetailsText { get; set; }
    /// <summary>Condo owner name.</summary>
    public string? CondoOwner { get; set; }
    /// <summary>Comma-joined machinery registration numbers.</summary>
    public string? MachineRegistrationNumbers { get; set; }
    /// <summary>Comma-joined Thai pricing-method labels.</summary>
    public string? PriceMethod { get; set; }
    /// <summary>
    /// Collateral value for the external company letter (ValuationAnalyses.AppraisedValue).
    /// Distinct from <see cref="TotalAppraisalValue"/> (the internal summary's group-sum total);
    /// the external branch populates this one. Fire-insurance value reuses
    /// <see cref="BuildingCoverageAmount"/> (same ValuationAnalyses.InsuranceValue source).
    /// </summary>
    public decimal? CollateralValue { get; set; }
    /// <summary>Surveyor / field appraiser name (ผู้สำรวจ) on the company letter.</summary>
    public string? SurveyorName { get; set; }
    /// <summary>Checker name (ผู้ตรวจสอบรายงาน) on the company-letter signature block.</summary>
    public string? CheckerName { get; set; }
    /// <summary>Verifier name (ผู้ประเมินหลักชั้นวุฒิ) on the company-letter signature block.</summary>
    public string? VerifyName { get; set; }
    /// <summary>Verifier license number.</summary>
    public string? VerifyLicenseNo { get; set; }
    /// <summary>Director / authorized signatory name.</summary>
    public string? DirectorName { get; set; }

    // ── Composite section props (rendered by the unified appraisal-book template) ──
    // Set only by AppraisalBookDataProvider (loaded once for every variant).
    // Standalone summary providers leave all of these null/empty (zero behaviour change).

    /// <summary>
    /// Land + Building details (§2.1.2.4 / §2.1.2.5), grouped by property group and rendered
    /// group-major ("กลุ่มที่ N → its land(s) → its building(s)"). Empty when neither present.
    /// </summary>
    public IReadOnlyList<PropertyGroupDetail> PropertyGroups { get; set; } = [];

    /// <summary>Construction progress section (§2.1.2.6). Null when not included in this report.</summary>
    public ConstructionSection? ConstructionSection { get; set; }

    /// <summary>
    /// Market comparison sections (§2.1.2.8), one per rendering group.
    /// For the comparison section the loader returns a list of 1 (all comparables share one section).
    /// Empty when no comparables exist for this appraisal.
    /// </summary>
    public IReadOnlyList<ComparisonSection> ComparisonSections { get; set; } = [];

    /// <summary>
    /// WQS price-analysis sections (§2.1.2.9), one per property group that uses the WQS method.
    /// Empty when no WQS method exists for this appraisal.
    /// </summary>
    public IReadOnlyList<WqsSection> WqsSections { get; set; } = [];

    /// <summary>
    /// Sale grid / direct comparison sections (§2.1.2.10), one per property group.
    /// Empty when no SaleGrid / DirectComparison method exists.
    /// </summary>
    public IReadOnlyList<SaleGridSection> SaleGridSections { get; set; } = [];

    /// <summary>Condo details section (§2.1.2.3). Null when not included in this report.</summary>
    public CondoSection? CondoSection { get; set; }

    /// <summary>Machine details section (§2.1.2.7). Null when not included in this report.</summary>
    public MachineSection? MachineSection { get; set; }

    /// <summary>
    /// Cost approach – machinery sections (§2.1.2.11), one per rendering group.
    /// For the cost-machine section the loader returns a list of 1 (internal grouping is per MachineCostGroup).
    /// Empty when no MachineryCost items exist.
    /// </summary>
    public IReadOnlyList<CostMachineSection> CostMachineSections { get; set; } = [];

    // ── Part B — new pricing-method sections ─────────────────────────────────────

    /// <summary>
    /// Income (DCF) sections (§2.1.2.xx), one per property group that uses the Income method.
    /// Empty when no Income method exists for this appraisal.
    /// </summary>
    public IReadOnlyList<IncomeSection> IncomeSections { get; set; } = [];

    /// <summary>
    /// Profit Rent sections, one per property group that uses the ProfitRent method.
    /// Empty when no ProfitRent method exists for this appraisal.
    /// </summary>
    public IReadOnlyList<ProfitRentSection> ProfitRentSections { get; set; } = [];

    /// <summary>
    /// Leasehold sections, one per property group that uses the Leasehold method.
    /// Empty when no Leasehold method exists for this appraisal.
    /// </summary>
    public IReadOnlyList<LeaseholdSection> LeaseholdSections { get; set; } = [];

    /// <summary>
    /// Hypothesis (residual) sections, one per property group that uses the Hypothesis method.
    /// Empty when no Hypothesis method exists for this appraisal.
    /// </summary>
    public IReadOnlyList<HypothesisSection> HypothesisSections { get; set; } = [];

    /// <summary>Appendix image section (§2.1.2.12+). Null when no image entries exist.</summary>
    public AppendixSection? AppendixSection { get; set; }

    /// <summary>
    /// PDF document IDs keyed by slot name (e.g. "appendix-0", one per appendix group).
    /// Merged at the corresponding &lt;!-- SLOT: name --&gt; marker by PdfSharpAssembler,
    /// so each group's PDFs land under that group's heading.
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

    // ── Cost-approach per-item breakdown (land-building standard body) ─────────────
    // Populated only for the land-building summary. Condo/Machine leave these default
    // and keep using the flat fields above.

    /// <summary>True when this group was valued by the Cost approach (per-item breakdown shown).</summary>
    public bool IsCostApproach { get; init; }

    /// <summary>True when the group contains land (renders ☑ ที่ดิน).</summary>
    public bool HasLand { get; init; }

    /// <summary>True when the group contains buildings (renders ☑ สิ่งปลูกสร้าง).</summary>
    public bool HasBuilding { get; init; }

    /// <summary>Land title description (โฉนด…), without the building clause.</summary>
    public string? LandDescription { get; init; }

    /// <summary>One entry per land title — rendered as separate lines in every land row.</summary>
    public List<string> LandDescriptions { get; init; } = [];

    /// <summary>Building clause (พร้อม…), newline-joined per building. Shown after the land
    /// titles in the market/combined land row (separated from the land title list).</summary>
    public string? BuildingDescription { get; init; }

    /// <summary>Per-item detail lines (e.g. each machine) — rendered as a numbered list.</summary>
    public List<string> DetailItems { get; init; } = [];

    /// <summary>Total land area in square-wa (rai×400 + ngan×100 + sqwa). Cost approach only.</summary>
    public decimal? TotalSquareWa { get; init; }

    // ── Market/combined land columns ──────────────────────────────────────────────
    // Separate from TotalSquareWa/LandUnitPrice above, which the cost branch owns.

    /// <summary>True when the market/combined row should render พื้นที่ + ราคาต่อหน่วย —
    /// a land-only group priced at a per-unit rate (PerSqWa/PerSqm).</summary>
    public bool ShowLandUnitColumns { get; init; }

    /// <summary>Land area in square-wa for the market/combined row.</summary>
    public decimal? MarketLandArea { get; init; }

    /// <summary>Land rate per square-wa / square-metre for the market/combined row
    /// (PricingAnalysisMethods.ValuePerUnit, else PricingFinalValues.FinalValueAdjusted).</summary>
    public decimal? MarketLandUnitPrice { get; init; }

    /// <summary>Land price per square-wa (PricingFinalValues.FinalValueAdjusted). Cost approach only.</summary>
    public decimal? LandUnitPrice { get; init; }

    /// <summary>Land appraised value (PricingFinalValues.LandValue). Cost approach only.</summary>
    public decimal? LandValue { get; init; }

    /// <summary>Building line items (BuildingDepreciationDetails where IsBuilding=1). Cost approach only.</summary>
    public IReadOnlyList<SummaryItemRow> Buildings { get; init; } = [];

    /// <summary>Development/improvement items (ส่วนพัฒนา; IsBuilding=0). Cost approach only.</summary>
    public IReadOnlyList<SummaryItemRow> DevelopmentItems { get; init; } = [];

    /// <summary>Group total value (รวมมูลค่าทรัพย์สินกลุ่ม). Also the combined value for market groups.</summary>
    public decimal? GroupTotal { get; init; }
}

/// <summary>One line item in a cost-approach group breakdown (a building or development item).</summary>
public sealed class SummaryItemRow
{
    /// <summary>Display description, e.g. "อาคารโรงงานชั้นเดียว พื้นที่ใช้สอย 1,800 ตารางเมตร อายุ 9 ปี".</summary>
    public string? Description { get; init; }

    /// <summary>Appraised value (PriceAfterDepreciation).</summary>
    public decimal? Value { get; init; }
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
