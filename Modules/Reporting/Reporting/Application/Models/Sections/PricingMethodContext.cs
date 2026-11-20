namespace Reporting.Application.Models.Sections;

/// <summary>
/// Where a printed pricing-method page sits in the valuation: which approach it belongs to, which
/// part of the property it values, and whether it fed the final value or is only a reference.
/// Printed as the one-line banner at the top of every pricing page (partials/book-method-banner).
/// </summary>
public sealed class PricingMethodContext
{
    /// <summary>"Market" / "Cost" / "Income" / "Residual" — PricingAnalysisApproaches.ApproachType.</summary>
    public string? ApproachType { get; init; }

    /// <summary>Thai name of the approach (วิธีเปรียบเทียบราคาตลาด, วิธีต้นทุน, …).</summary>
    public string? ApproachLabel { get; init; }

    /// <summary>Thai name of the part valued (ที่ดิน / สิ่งปลูกสร้าง / ที่ดินพร้อมสิ่งปลูกสร้าง), null when the method has no role.</summary>
    public string? RoleLabel { get; init; }

    /// <summary>True when the method's approach is NOT the selected one — analysed, kept for reference ("(อ้างอิง)").</summary>
    public bool IsReference { get; init; }

    /// <summary>PricingAnalysisMethods.Remark — the appraiser's "หมายเหตุและสมมติฐาน" for this method.</summary>
    public string? Remark { get; init; }
}
