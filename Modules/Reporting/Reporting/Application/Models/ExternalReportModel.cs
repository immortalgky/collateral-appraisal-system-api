using Reporting.Application.Models.Sections;

namespace Reporting.Application.Models;

/// <summary>
/// ViewModel for the "รายงานประเมินมูลค่าทรัพย์สิน" (External Appraisal Report),
/// FSD §2.1.2 — เล่มรายงานประเมินบริษัท.
///
/// Covers:
///   §2.1.2.1  Cover Page
///   §2.1.2.2  Company Letter
///   Appendix  SLOT (AttachmentsBySlot — populated in a later phase)
///
/// All reference types nullable; collections default to empty.
/// </summary>
public sealed class ExternalReportModel
{
    // ── External Company (from auth.Companies via AppraisalAssignments) ──────────

    /// <summary>Company name (auth.Companies.Name).</summary>
    public string? CompanyName { get; init; }

    /// <summary>
    /// Composed address: Street + City + Province + PostalCode (blanks skipped).
    /// </summary>
    public string? CompanyAddress { get; init; }

    /// <summary>Company telephone (auth.Companies.Phone).</summary>
    public string? CompanyTel { get; init; }

    // ── Customer ─────────────────────────────────────────────────────────────────

    /// <summary>Customer names joined with " และ " (request.RequestCustomers.Name).</summary>
    public string? CustomerName { get; init; }

    // ── Appraisal identification ──────────────────────────────────────────────────

    /// <summary>Appraisal book number (appraisal.Appraisals.AppraisalNumber).</summary>
    public string? AppraisalBookNumber { get; init; }

    // ── Cover / letter body ───────────────────────────────────────────────────────

    /// <summary>
    /// Property-type summary string per FSD field 4 format:
    /// "{PropertyTypeThai} จำนวน {N} {unit} เนื้อที่ {area} {areaUnit}"
    /// Comma-joined when multiple types exist for the appraisal.
    /// </summary>
    public string? PropertyTypeSummary { get; init; }

    /// <summary>
    /// Composed collateral location via ThaiAddressFormatter.
    /// Condo → FormatCondo; Land/other → FormatLandBuilding.
    /// </summary>
    public string? CollateralLocation { get; init; }

    /// <summary>
    /// Literal fixed value: "ธนาคารแลนด์ แอนด์ เฮ้าส์ จำกัด (มหาชน)".
    /// Stored on the model so Scriban templates reference it uniformly.
    /// </summary>
    public string BankName { get; init; } = "ธนาคารแลนด์ แอนด์ เฮ้าส์ จำกัด (มหาชน)";

    /// <summary>
    /// Report/verification date.
    /// Source: appraisal.Appraisals.CompletedAt (external verify completion).
    /// Falls back to latest appointment date when CompletedAt is null.
    /// </summary>
    public DateTime? VerifyDate { get; init; }

    /// <summary>
    /// Literal fixed value: "แจ้งผลการประเมินมูลค่าทรัพย์สิน".
    /// Kept on the model for Scriban access; never changes.
    /// </summary>
    public string Subject { get; init; } = "แจ้งผลการประเมินมูลค่าทรัพย์สิน";

    // ── GPS ───────────────────────────────────────────────────────────────────────

    /// <summary>GPS coordinates string "Lat : {lat}  Lon : {lon}" or null.</summary>
    public string? Gps { get; init; }

    // ── Land title deeds ─────────────────────────────────────────────────────────

    /// <summary>Comma-joined TitleNumbers from appraisal.LandTitles.</summary>
    public string? TitleDeedNumbers { get; init; }

    /// <summary>Total count of land title deeds.</summary>
    public int? TotalTitleDeeds { get; init; }

    /// <summary>
    /// Land area formatted as "{rai} - {ngan} - {wa} ไร่ หรือ {totalSqWa} ตารางวา".
    /// Computed from SUM(AreaRai/AreaNgan/AreaSquareWa) across all LandTitles.
    /// </summary>
    public string? LandAreaText { get; init; }

    // ── Ownership ────────────────────────────────────────────────────────────────

    /// <summary>Land owner (LandAppraisalDetails.OwnerName). Null when no land properties.</summary>
    public string? LandOwner { get; init; }

    /// <summary>
    /// Building details text: "{BuildingType-Thai} {NumberOfFloors} ชั้น แบบ {ModelName}".
    /// Null when no building properties.
    /// </summary>
    public string? BuildingDetailsText { get; init; }

    /// <summary>Building owner (BuildingAppraisalDetails.OwnerName). Null when no building properties.</summary>
    public string? BuildingOwner { get; init; }

    /// <summary>Condo owner (CondoAppraisalDetails.OwnerName). Null when no condo properties.</summary>
    public string? CondoOwner { get; init; }

    /// <summary>
    /// Comma-joined RegistrationNumbers from MachineryAppraisalDetails.
    /// Null when no machinery properties.
    /// </summary>
    public string? MachineRegistrationNumbers { get; init; }

    // ── Legal ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Obligation / encumbrance details.
    /// Sources: LandAppraisalDetails.ObligationDetails ?? CondoAppraisalDetails.ObligationDetails.
    /// </summary>
    public string? Obligation { get; init; }

    /// <summary>
    /// City Planning Act / urban planning colour.
    /// No clean column in current schema — null (deferred).
    /// </summary>
    public string? CityPlanningAct { get; init; }

    // ── Appraisal purpose ────────────────────────────────────────────────────────

    /// <summary>
    /// Literal: "เพื่อใช้ในการพิจารณาขอสินเชื่อของ ธนาคารแลนด์ แอนด์ เฮ้าส์ จำกัด (มหาชน)".
    /// </summary>
    public string AppraisalPurpose { get; init; }
        = "เพื่อใช้ในการพิจารณาขอสินเชื่อของ ธนาคารแลนด์ แอนด์ เฮ้าส์ จำกัด (มหาชน)";

    // ── Valuation method ─────────────────────────────────────────────────────────

    /// <summary>
    /// Comma-joined Thai method labels derived from distinct PricingAnalysisMethods.MethodType
    /// (e.g. "วิธีเปรียบเทียบราคาตลาด (WQS), วิธีต้นทุน").
    /// </summary>
    public string? PriceMethod { get; init; }

    // ── Dates ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inspection / appointment date (latest non-cancelled Appointments.AppointmentDateTime).
    /// Used as "วันที่ประเมินมูลค่า" in the letter table.
    /// </summary>
    public DateTime? AppraisalDate { get; init; }

    // ── Values ───────────────────────────────────────────────────────────────────

    /// <summary>Collateral value (ValuationAnalyses.AppraisedValue).</summary>
    public decimal? CollateralValue { get; init; }

    /// <summary>Forced sale value (ValuationAnalyses.ForcedSaleValue).</summary>
    public decimal? ForcedSaleValue { get; init; }

    /// <summary>Fire insurance value (ValuationAnalyses.InsuranceValue).</summary>
    public decimal? FireInsuranceValue { get; init; }

    // ── Signature block ───────────────────────────────────────────────────────────

    /// <summary>
    /// Surveyor / field appraiser name (ผู้สำรวจ).
    /// Source: AppraisalAssignments.ExternalAppraiserName for the external assignment.
    /// </summary>
    public string? SurveyorName { get; init; }

    /// <summary>
    /// Checker name (ผู้ตรวจสอบรายงาน).
    /// No dedicated source column in current schema — null (deferred).
    /// </summary>
    public string? CheckerName { get; init; }

    /// <summary>
    /// Verifier name (ผู้ประเมินหลักชั้นวุฒิ).
    /// No dedicated source column in current schema — null (deferred).
    /// </summary>
    public string? VerifyName { get; init; }

    /// <summary>
    /// Verifier license number.
    /// No dedicated source column in current schema — null (deferred).
    /// </summary>
    public string? VerifyLicenseNo { get; init; }

    /// <summary>
    /// Director / authorized signatory name (กรรมการผู้มีอำนาจลงนาม).
    /// No dedicated source column in current schema — null (deferred).
    /// </summary>
    public string? DirectorName { get; init; }

    // ── Detail sections (§2.1.2.3–2.1.2.7; each null when that collateral type absent) ──

    /// <summary>Land details section (§2.1.2.4) — null when no land properties.</summary>
    public LandSection? LandSection { get; init; }

    /// <summary>Building details section (§2.1.2.5) — null when no building properties.</summary>
    public BuildingSection? BuildingSection { get; init; }

    /// <summary>Condo details section (§2.1.2.3) — null when no condo properties.</summary>
    public CondoSection? CondoSection { get; init; }

    /// <summary>Construction progress section (§2.1.2.6) — null when no construction inspection.</summary>
    public ConstructionSection? ConstructionSection { get; init; }

    /// <summary>Machine details section (§2.1.2.7) — null when no machinery.</summary>
    public MachineSection? MachineSection { get; init; }

    // ── Price-analysis sections (§2.1.2.8–2.1.2.11; null when that method/data absent) ──

    /// <summary>Comparison information (§2.1.2.8) — null when no linked comparables.</summary>
    public ComparisonSection? ComparisonSection { get; init; }

    /// <summary>WQS analysis (§2.1.2.9) — null when no WQS method.</summary>
    public WqsSection? WqsSection { get; init; }

    /// <summary>Sale-Grid / Direct-Comparison (§2.1.2.10) — null when no such method.</summary>
    public SaleGridSection? SaleGridSection { get; init; }

    /// <summary>Cost approach – machinery (§2.1.2.11) — null when no MachineryCost items.</summary>
    public CostMachineSection? CostMachineSection { get; init; }

    // ── Appendix (§2.1.2.12+) ────────────────────────────────────────────────────

    /// <summary>
    /// Image appendix groups (ภาคผนวก) — null when no image-type appendix entries exist.
    /// PDF appendix entries are handled via AttachmentsBySlot["appendix"].
    /// </summary>
    public AppendixSection? AppendixSection { get; init; }

    // ── Appendix slot ────────────────────────────────────────────────────────────

    /// <summary>
    /// Attachment Guids keyed by slot name (e.g. "appendix").
    /// Populated by <c>AppendixSectionLoader</c>: PDF appendix DocumentIds are placed
    /// under the "appendix" key and merged by <c>PdfSharpAssembler</c> at the
    /// <c>&lt;!-- SLOT: appendix --&gt;</c> marker in the template.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Guid>> AttachmentsBySlot { get; init; }
        = new Dictionary<string, IReadOnlyList<Guid>>();
}
