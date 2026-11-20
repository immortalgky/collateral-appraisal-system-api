namespace Reporting.Application.Models;

/// <summary>
/// One analysed approach in the company letter's "มูลค่าแต่ละแนวทาง" table. The approach the valuation used is
/// marked <see cref="IsReference"/> = false; every other analysed approach is a reference ("(อ้างอิง)").
/// </summary>
public sealed class ApproachValueRow
{
    public int GroupNumber { get; init; }
    public string? GroupName { get; init; }

    /// <summary>"Market" / "Cost" / "Income" / "Residual" — PricingAnalysisApproaches.ApproachType.</summary>
    public string? ApproachType { get; init; }

    /// <summary>Thai approach name (วิธีเปรียบเทียบราคาตลาด, วิธีต้นทุน, …).</summary>
    public string? ApproachLabel { get; init; }

    /// <summary>The methods behind the value (type and role), in the picker's order: what the section 5 narrative names. Empty when none is known.</summary>
    public IReadOnlyList<ApproachMethodRef> Methods { get; init; } = [];

    /// <summary>Section 6: the bold title line above this approach's intro paragraph (see PricingNarrative).</summary>
    public string IntroTitle => Reporting.Application.Providers.PricingNarrative.IntroTitle(this);

    /// <summary>Section 6: this approach's intro paragraph, in prose, from the methods behind its value (see PricingNarrative).</summary>
    public string IntroText => Reporting.Application.Providers.PricingNarrative.IntroText(this);

    public decimal? Value { get; init; }
    public bool IsReference { get; init; }
}

/// <summary>One method that gave (part of) an approach's value: PricingAnalysisMethods.MethodType (WQS, SaleGrid, BuildingCost ...) and its Role (Land, Building, LandAndBuilding, Machinery; null = the whole property).</summary>
public sealed record ApproachMethodRef(string MethodType, string? Role);
