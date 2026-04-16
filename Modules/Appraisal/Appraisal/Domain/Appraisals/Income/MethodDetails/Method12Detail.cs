using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 12 — Proportion Of New Replacement Cost (% of reconstruction cost).</summary>
public sealed record Method12Detail
{
    [JsonPropertyName("proportionPct")]
    public decimal ProportionPct { get; init; }

    [JsonPropertyName("increaseRatePct")]
    public decimal IncreaseRatePct { get; init; }

    [JsonPropertyName("increaseRateYrs")]
    public int IncreaseRateYrs { get; init; }

    // Table
    [JsonPropertyName("newReplacementCost")]
    public decimal NewReplacementCost { get; init; }

    [JsonPropertyName("proportionOfNewReplacementCosts")]
    public decimal[] ProportionOfNewReplacementCosts { get; init; } = [];
}
