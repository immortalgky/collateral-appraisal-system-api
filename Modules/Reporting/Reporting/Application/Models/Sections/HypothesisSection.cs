namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "วิธีสมมติฐาน (Hypothesis)" pricing section.
///
/// Source: appraisal.HypothesisAnalyses (FK: PricingMethodId, unique).
/// Confirmed against HypothesisAnalysisConfiguration.cs:
///   Table = "HypothesisAnalyses" (schema appraisal)
///   FK column: PricingMethodId (unique index)
///   Variant stored as int (HasConversion&lt;int&gt;): 1 = LandBuilding, 2 = Condominium
///
/// LandBuildingSummary owned columns — EF auto-names as LandBuildingSummary_{PropertyName}
/// (no HasColumnName overrides except Remark). Key fields: C15–C82.
///
/// CondominiumSummary owned columns — EF auto-names as CondominiumSummary_{PropertyName}
/// (no HasColumnName overrides except Remark). Key fields: E13–E59.
///
/// The template renders only the block matching Variant:
///   Variant=1 → LandBuilding summary (C-row fields)
///   Variant=2 → Condominium summary (E-row fields)
/// </summary>
public sealed class HypothesisSection
{
    // ── Property group routing ────────────────────────────────────────────────────

    /// <summary>PropertyGroups.GroupNumber for the group this Hypothesis method belongs to.</summary>
    public int GroupNumber { get; init; }

    /// <summary>PropertyGroups.GroupName, or null when not set.</summary>
    public string? GroupName { get; init; }

    // ── Discriminator ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Hypothesis variant. 1 = LandBuilding (Village / Housing Project), 2 = Condominium.
    /// Source: HypothesisAnalyses.Variant (stored as int).
    /// </summary>
    public int Variant { get; init; }

    // ── LandBuilding Summary (Variant = 1) ─────────────────────────────────────
    // Column names: LandBuildingSummary_{PropertyName} (EF auto-named; confirmed)

    /// <summary>C15 — รายรับรวม (บาท).</summary>
    public decimal? LbTotalRevenue { get; init; }

    /// <summary>C38 — ต้นทุนพัฒนาโครงการรวม (บาท).</summary>
    public decimal? LbTotalProjectDevCost { get; init; }

    /// <summary>C72 — ภาษีรัฐบาลรวม (บาท).</summary>
    public decimal? LbTotalGovTax { get; init; }

    /// <summary>C74 — ค่าความเสี่ยง / กำไร (%).</summary>
    public decimal? LbRiskPremiumPercent { get; init; }

    /// <summary>C75 — ค่าความเสี่ยง / กำไร (บาท).</summary>
    public decimal? LbRiskPremiumAmount { get; init; }

    /// <summary>C76 — ต้นทุนพัฒนาและค่าใช้จ่ายรวม (บาท).</summary>
    public decimal? LbTotalDevCostsAndExpenses { get; init; }

    /// <summary>C77 — มูลค่าทรัพย์สินปัจจุบัน (บาท).</summary>
    public decimal? LbCurrentPropertyValue { get; init; }

    /// <summary>C78 — อัตราคิดลด (%).</summary>
    public decimal? LbDiscountRate { get; init; }

    /// <summary>C79 — ตัวคูณลด.</summary>
    public decimal? LbDiscountRateFactor { get; init; }

    /// <summary>C80 — มูลค่าทรัพย์สินสุทธิ (บาท).</summary>
    public decimal? LbFinalPropertyValue { get; init; }

    /// <summary>C81 — มูลค่าทรัพย์สิน (ปัดเศษ) (บาท).</summary>
    public decimal? LbTotalAssetValueRounded { get; init; }

    /// <summary>C82 — มูลค่าต่อตารางวา (บาท/ตร.วา).</summary>
    public decimal? LbTotalAssetValuePerSqWa { get; init; }

    // ── Condominium Summary (Variant = 2) ──────────────────────────────────────
    // Column names: CondominiumSummary_{PropertyName} (EF auto-named; confirmed)

    /// <summary>E13 — รายรับรวม (บาท).</summary>
    public decimal? CndTotalRevenue { get; init; }

    /// <summary>E27 — ต้นทุนก่อสร้างรวม (บาท).</summary>
    public decimal? CndTotalHardCost { get; init; }

    /// <summary>E45 — ต้นทุนอ่อนรวม (บาท).</summary>
    public decimal? CndTotalSoftCost { get; init; }

    /// <summary>E50 — ภาษีรัฐบาลรวม (บาท).</summary>
    public decimal? CndTotalGovTax { get; init; }

    /// <summary>E51 — กำไร / ความเสี่ยง (%).</summary>
    public decimal? CndRiskProfitPercent { get; init; }

    /// <summary>E52 — กำไร / ความเสี่ยง (บาท).</summary>
    public decimal? CndRiskProfitTotal { get; init; }

    /// <summary>E53 — ต้นทุนพัฒนารวม (บาท).</summary>
    public decimal? CndTotalDevCosts { get; init; }

    /// <summary>E54 — มูลค่าเหลือสุทธิ (บาท).</summary>
    public decimal? CndTotalRemainingValue { get; init; }

    /// <summary>E55 — อัตราคิดลด (%).</summary>
    public decimal? CndDiscountRate { get; init; }

    /// <summary>E56 — ตัวคูณลด.</summary>
    public decimal? CndDiscountRateFactor { get; init; }

    /// <summary>E57 — มูลค่าเหลือสุทธิหลังคิดลด (บาท).</summary>
    public decimal? CndFinalRemainingValue { get; init; }

    /// <summary>E58 — มูลค่าทรัพย์สิน (ปัดเศษ) (บาท).</summary>
    public decimal? CndTotalAssetValueRounded { get; init; }

    /// <summary>E59 — มูลค่าต่อตารางเมตร (บาท/ตร.ม.).</summary>
    public decimal? CndTotalAssetValuePerSqM { get; init; }
}
