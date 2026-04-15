using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 11 — Specified Energy Cost Index with Growth.</summary>
public sealed record Method11Detail
{
    [JsonPropertyName("energyCostIndex")]
    public decimal EnergyCostIndex { get; init; }

    [JsonPropertyName("increaseRatePct")]
    public decimal IncreaseRatePct { get; init; }

    [JsonPropertyName("increaseRateYrs")]
    public int IncreaseRateYrs { get; init; }

    // Table (year-indexed arrays)
    [JsonPropertyName("increaseRate")]
    public decimal[] IncreaseRate { get; init; } = [];

    [JsonPropertyName("energyCostIndexIncrease")]
    public decimal[] EnergyCostIndexIncrease { get; init; } = [];

    [JsonPropertyName("totalEnegyCost")]
    public decimal[] TotalEnergyCost { get; init; } = [];
}
