namespace Reporting.Application.Models;

/// <summary>
/// Strongly-typed ViewModel for the "ใบขอนัดสำรวจและประเมินราคา"
/// (Appointment and Quotation Request Form).
/// All string fields are nullable so partial data still renders.
/// </summary>
public sealed class AppointmentQuotationFormModel
{
    // ---------- Header / Bank internal fields ----------

    /// <summary>Division / สาขา / หน่วยงาน</summary>
    public string? Division { get; init; }

    /// <summary>Department / ฝ่าย</summary>
    public string? Department { get; init; }

    /// <summary>Line-of-Work / งาน</summary>
    public string? LineOfWork { get; init; }

    /// <summary>Cost Center / รหัสศูนย์ต้นทุน</summary>
    public string? CostCenter { get; init; }

    // ---------- Referrer ----------
    public string? ReferrerName { get; init; }
    public string? ReferrerTel { get; init; }

    // ---------- Customer ----------
    public string? CustomerName { get; init; }

    // ---------- Contact person at the collateral location ----------
    public string? ContactPersonName { get; init; }
    public string? ContactPersonTel { get; init; }

    // ---------- Appraisal purpose & loan ----------
    /// <summary>Appraisal purpose code, e.g. "REQUEST_FOR_CREDIT_LIMIT"</summary>
    public string? AppraisalPurpose { get; init; }

    /// <summary>Loan / Facility amount (THB)</summary>
    public decimal? LoanAmount { get; init; }

    // ---------- Collateral properties (repeating block) ----------
    /// <summary>One row per collateral item in the appraisal request.</summary>
    public IReadOnlyList<PropertyRow> Properties { get; init; } = Array.Empty<PropertyRow>();

    // ---------- Collateral address (primary location) ----------
    public string? HouseNumber { get; init; }
    public string? ProjectName { get; init; }
    public string? Soi { get; init; }
    public string? Road { get; init; }
    public string? SubDistrict { get; init; }
    public string? District { get; init; }
    public string? Province { get; init; }

    // ---------- Second / mailing address ----------
    public string? Address2HouseNumber { get; init; }
    public string? Address2Soi { get; init; }
    public string? Address2Road { get; init; }
    public string? Address2SubDistrict { get; init; }
    public string? Address2District { get; init; }
    public string? Address2Province { get; init; }

    // ---------- Collateral status ----------
    /// <summary>true = New collateral; false = Existing collateral</summary>
    public bool IsNewCollateral { get; init; }

    // ---------- Fee ----------
    /// <summary>Fee payment type code, e.g. "BANK_ABSORB" / "CUSTOMER_PAY"</summary>
    public string? FeePaymentType { get; init; }

    // ---------- Requester maker ----------
    public string? RequesterMakerName { get; init; }
    public DateTime? RequestDate { get; init; }

    // ---------- Requester checker ----------
    public string? RequesterCheckerName { get; init; }
    public DateTime? CheckerDate { get; init; }

    // ---------- Old appraisal reference ----------
    public string? OldAppraisalReportNumber { get; init; }

    // ---------- Attachment slots ----------
    /// <summary>
    /// Maps slot name to list of document IDs that should be inserted at that
    /// slot marker in the rendered PDF.  Key "attachments" is the default appendix slot.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Guid>> AttachmentsBySlot { get; init; }
        = new Dictionary<string, IReadOnlyList<Guid>>();
}

/// <summary>One collateral item row in the appointment/quotation form table.</summary>
public sealed class PropertyRow
{
    public int RowNumber { get; init; }
    public string? PropertyType { get; init; }
    public string? BuildingType { get; init; }
    public string? Village { get; init; }
    public string? HouseNumber { get; init; }
    public string? Soi { get; init; }
    public string? Road { get; init; }
    public string? SubDistrict { get; init; }
    public string? District { get; init; }
    public string? Province { get; init; }
    /// <summary>Old appraisal report number for this specific property, if any.</summary>
    public string? OldReportNumber { get; init; }
}
