namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "วิธีสมมติฐาน (Hypothesis)" pricing section — one per printed Hypothesis method.
///
/// Source: appraisal.HypothesisAnalyses (FK PricingMethodId, unique) and its children. Laid out as the
/// pricing screen: the summary ledger (<see cref="Bands"/>, the same bands, rows and Thai labels as
/// LandBuildingSummaryTab / CondominiumSummaryTab), then the unit details (per-model analysis + the
/// uploaded unit listing).
///
/// Variant: 1 = LandBuilding (C01–C82), 2 = Condominium (E01–E59).
/// </summary>
public sealed class HypothesisSection
{
    /// <summary>PropertyGroups.GroupNumber for the group this Hypothesis method belongs to.</summary>
    public int GroupNumber { get; init; }

    /// <summary>PropertyGroups.GroupName, or null when not set.</summary>
    public string? GroupName { get; init; }

    /// <summary>Which approach / role this page belongs to and whether it is a reference (printed in the page banner).</summary>
    public PricingMethodContext? Context { get; init; }

    /// <summary>1 = LandBuilding (บ้านจัดสรร), 2 = Condominium.</summary>
    public int Variant { get; init; }

    /// <summary>The L&amp;B ledger has a สัดส่วน column; the condo one does not (same as the screen).</summary>
    public bool HasRatio { get; init; }

    /// <summary>The summary ledger, band by band. Cell strings are formatted with their unit.</summary>
    public IReadOnlyList<HypothesisLedgerBand> Bands { get; init; } = [];

    /// <summary>Per-model analysis (L&amp;B: house model, condo: room type) of the unit listing, in first-seen order.</summary>
    public IReadOnlyList<HypothesisModelRow> Models { get; init; } = [];

    /// <summary>The uploaded unit listing (active upload), in sequence order. Empty when nothing was uploaded.</summary>
    public IReadOnlyList<HypothesisUnitRow> Units { get; init; } = [];

    /// <summary>Totals of <see cref="Units"/> (count, land area, usable area, selling price).</summary>
    public HypothesisModelRow? UnitTotal { get; init; }
}

/// <summary>One band of the summary ledger (รายละเอียดพื้นที่ดิน, ประมาณการรายได้โครงการ, …).</summary>
public sealed class HypothesisLedgerBand
{
    public string Title { get; init; } = "";
    public IReadOnlyList<HypothesisLedgerRow> Rows { get; init; } = [];
}

/// <summary>
/// One ledger row. A null cell is a cell the row does not use (printed blank); a cell the row uses
/// but the record left empty holds "-".
/// </summary>
public sealed class HypothesisLedgerRow
{
    public string Label { get; init; } = "";

    /// <summary>"" = plain, "sub" = indented per-model line, "tot" = band total, "final" = method value, "remark" = full-width note.</summary>
    public string Kind { get; init; } = "";

    /// <summary>อัตรา / ค่าที่ตั้ง, e.g. "1,500.00 บาท/ตร.วา" or "5.00 % ของรายได้รวม".</summary>
    public string? Rate { get; init; }

    /// <summary>จำนวน, e.g. "2,000.00 ตร.วา" or "48 หน่วย".</summary>
    public string? Qty { get; init; }

    /// <summary>รวม (บาท) — or months / บาท/ตร.วา where the screen prints those in the total column.</summary>
    public string? Total { get; init; }

    /// <summary>สัดส่วน, e.g. "12.50 %" (L&amp;B only).</summary>
    public string? Ratio { get; init; }
}

/// <summary>One house model / room type of the unit listing, or the listing's total row.</summary>
public sealed class HypothesisModelRow
{
    public string Name { get; init; } = "";
    public int Units { get; init; }
    public decimal? AvgLandAreaSqWa { get; init; }
    public decimal? TotalLandAreaSqWa { get; init; }
    public decimal? AvgUsableAreaSqM { get; init; }
    public decimal? TotalUsableAreaSqM { get; init; }
    public decimal? AvgSellingPrice { get; init; }
    public decimal? TotalSellingPrice { get; init; }
}

/// <summary>One uploaded unit (HypothesisLandBuildingUnitRows / HypothesisCondominiumUnitRows).</summary>
public sealed class HypothesisUnitRow
{
    public int Sequence { get; init; }
    /// <summary>L&amp;B: house model (แบบบ้าน). Condo: room type (แบบห้อง).</summary>
    public string? Model { get; init; }
    public int? FloorNo { get; init; }
    public decimal? UsableAreaSqM { get; init; }
    public decimal? SellingPrice { get; init; }
    public string? Remark1 { get; init; }
    public string? Remark2 { get; init; }

    // L&B only
    public string? PlanNo { get; init; }
    public string? HouseNo { get; init; }
    public string? Location { get; init; }
    public decimal? LandAreaSqWa { get; init; }

    // Condo only
    public string? Building { get; init; }
    public string? AptNo { get; init; }
    public string? Apartment { get; init; }
}
