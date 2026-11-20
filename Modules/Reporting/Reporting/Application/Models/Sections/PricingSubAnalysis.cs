namespace Reporting.Application.Models.Sections;

/// <summary>
/// A market sub-analysis printed under the method that owns it: the WQS / Sale Grid / Direct Comparison the
/// appraiser ran to get one input of that method (leasehold land price, market rent, machine RCN, income land
/// value, room rate) or the price of one block-project model. Exactly one of <see cref="Wqs"/> /
/// <see cref="SaleGrid"/> is set; it renders with the same partial as a group's own WQS / Sale Grid page.
/// Source: appraisal.PricingAnalysis rows with SubjectType 1–6 (see PricingSubAnalysisLoader).
/// </summary>
public sealed class PricingSubAnalysis
{
    /// <summary>The sub-heading, e.g. "ที่มาของค่าเช่าตลาด". Consecutive entries with the same heading print it once.</summary>
    public string Heading { get; init; } = "";

    /// <summary>What this one analysis is for when the owner has several (a machine, a room type); null when there is only one subject.</summary>
    public string? Caption { get; init; }

    /// <summary>"วิธีให้คะแนนคุณภาพถ่วงน้ำหนัก (WQS)" / Sale Grid / Direct Comparison.</summary>
    public string MethodLabel { get; init; } = "";

    public WqsSection? Wqs { get; init; }
    public SaleGridSection? SaleGrid { get; init; }
}

/// <summary>One row of a "growth by period" table (LeaseholdLandGrowthPeriods / ProfitRentGrowthPeriods).</summary>
public sealed class GrowthPeriodRow
{
    public int FromYear { get; init; }
    public int ToYear { get; init; }
    public decimal GrowthRatePercent { get; init; }
}
