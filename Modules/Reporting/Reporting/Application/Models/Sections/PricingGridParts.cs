namespace Reporting.Application.Models.Sections;

/// <summary>A comparable's column header on the pricing screens: "ตลาด n" plus the sub-line "ประกาศขาย · 3/11/2026 · /ตร.ว.".</summary>
public sealed class MarketHeader
{
    public string Title { get; init; } = "";
    public string? Sub { get; init; }
}

/// <summary>A slice of the comparable columns (Start, Count) that fits one A4 portrait table; factor and collateral columns repeat per table.</summary>
public sealed class TableChunk
{
    public int Start { get; init; }
    public int Count { get; init; }
}

/// <summary>One row of the "ปรับมูลค่าสุดท้าย (Adjust Final Value)" block, read from PricingFinalValues.</summary>
public sealed class PricingFinalRow
{
    public string Label { get; init; } = "";
    public string? Value { get; init; }
    public string? Unit { get; init; }

    /// <summary>"แก้เอง · ต่างจากที่คำนวณ +x" when the appraiser's figure differs from the computed one; null otherwise.</summary>
    public string? Note { get; init; }
}
