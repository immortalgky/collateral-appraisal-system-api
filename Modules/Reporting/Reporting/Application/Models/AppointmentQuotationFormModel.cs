namespace Reporting.Application.Models;

/// <summary>
/// Strongly-typed ViewModel for the "ใบขอนัดสำรวจและประเมินราคา"
/// (Survey Appointment / Quotation Request form, FSD Ch.10 §2.1.1, img3/img4).
///
/// The form is a fill-in document: every field is nullable and renders as a blank
/// underline when no data is available. Fields without a backing Request column are
/// intentionally left unset (the requestor org block, the administrative-jurisdiction
/// address, and the checker) — see <see cref="Providers.AppointmentQuotationDataProvider"/>.
/// </summary>
public sealed class AppointmentQuotationFormModel
{
    // ── Requestor org block (FSD 1–4) — no Request column today, render blank ──
    /// <summary>ส่วน (Section) — FSD field 1.</summary>
    public string? Division { get; init; }

    /// <summary>ฝ่าย (Department) — FSD field 2.</summary>
    public string? Department { get; init; }

    /// <summary>สายงาน (Line of work) — FSD field 3.</summary>
    public string? LineOfWork { get; init; }

    /// <summary>Cost Center — FSD field 4.</summary>
    public string? CostCenter { get; init; }

    // ── Referrer (FSD 5–6) — no referrer field in Request, render blank ──
    /// <summary>ผู้แนะนำ (Referrer) — FSD field 5.</summary>
    public string? ReferrerName { get; init; }

    /// <summary>โทรศัพท์ ผู้แนะนำ — FSD field 6.</summary>
    public string? ReferrerTel { get; init; }

    // ── Customer (FSD 7) ──
    public string? CustomerName { get; init; }

    // ── Contact person (FSD 9–10) ──
    public string? ContactPersonName { get; init; }
    public string? ContactPersonTel { get; init; }

    // ── Purpose & loan (FSD 11–12) ──
    /// <summary>วัตถุประสงค์การประเมิน — FSD field 11.</summary>
    public string? AppraisalPurpose { get; init; }

    /// <summary>จำนวน / วงเงิน (THB) — FSD field 12.</summary>
    public decimal? LoanAmount { get; init; }

    // ── Collateral status (FSD 8) ──
    /// <summary>true = หลักประกันใหม่ (New); false = หลักประกันเดิม (Existing).</summary>
    public bool IsNewCollateral { get; init; }

    // ── Collateral detail (FSD 13–14) — free text, NOT a table ──
    /// <summary>รายละเอียดทรัพย์สิน — FSD field 13 (distinct property types, joined).</summary>
    public string? PropertyDetail { get; init; }

    /// <summary>ประเภทหลักประกัน — FSD field 14 (distinct building types, joined).</summary>
    public string? CollateralType { get; init; }

    // ── Property location per title deed — ที่ตั้งทรัพย์สิน (ตามโฉนด), FSD 15–21 ──
    /// <summary>หมู่บ้าน / โครงการ — FSD field 15.</summary>
    public string? ProjectName { get; init; }

    /// <summary>เลขที่ — FSD field 16.</summary>
    public string? HouseNumber { get; init; }

    /// <summary>ซอย — FSD field 17.</summary>
    public string? Soi { get; init; }

    /// <summary>ถนน — FSD field 18.</summary>
    public string? Road { get; init; }

    /// <summary>แขวง/ตำบล — FSD field 19.</summary>
    public string? SubDistrict { get; init; }

    /// <summary>เขต/อำเภอ — FSD field 20.</summary>
    public string? District { get; init; }

    /// <summary>จังหวัด — FSD field 21.</summary>
    public string? Province { get; init; }

    // ── Property location per administrative jurisdiction — (ตามเขตปกครอง), FSD 22–28 ──
    // Only ONE address exists in the Request schema, so these have no distinct source
    // and render as blank underlines.
    public string? AdminProjectName { get; init; }
    public string? AdminHouseNumber { get; init; }
    public string? AdminSoi { get; init; }
    public string? AdminRoad { get; init; }
    public string? AdminSubDistrict { get; init; }
    public string? AdminDistrict { get; init; }
    public string? AdminProvince { get; init; }

    // ── Old appraisal report number (FSD 29) ──
    public string? OldAppraisalReportNumber { get; init; }

    // ── Fee payment type (FSD 30) ──
    public string? FeePaymentType { get; init; }

    // ── Maker (FSD 31–32) ──
    public string? RequesterMakerName { get; init; }
    public DateTime? RequestDate { get; init; }

    // ── Checker, manager-level (FSD 33–34) — no checker concept on Request, render blank ──
    public string? RequesterCheckerName { get; init; }
    public DateTime? CheckerDate { get; init; }

    // ── Page 2: document checklist (FSD img4) ──
    /// <summary>
    /// One section per FSD collateral-type row; each item is ticked when a matching document
    /// was uploaded at the title level for that collateral type.
    /// </summary>
    public IReadOnlyList<ChecklistSection> ChecklistSections { get; init; } = Array.Empty<ChecklistSection>();

    // ── Attachment slots ──
    /// <summary>
    /// Maps slot name to document IDs inserted at that slot marker in the rendered PDF.
    /// Key "attachments" is the appendix slot appended after the form pages.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Guid>> AttachmentsBySlot { get; init; }
        = new Dictionary<string, IReadOnlyList<Guid>>();
}

/// <summary>One collateral-type row of the page-2 document checklist.</summary>
public sealed class ChecklistSection
{
    public string CollateralLabel { get; init; } = "";
    public IReadOnlyList<ChecklistItem> Items { get; init; } = Array.Empty<ChecklistItem>();
}

/// <summary>One document line in a checklist section; ticked when uploaded.</summary>
public sealed class ChecklistItem
{
    public string Label { get; init; } = "";
    public bool IsChecked { get; init; }
}
