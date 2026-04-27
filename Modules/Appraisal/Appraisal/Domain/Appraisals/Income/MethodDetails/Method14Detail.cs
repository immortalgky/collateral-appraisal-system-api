using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 14 — Specified Value With Growth (generic first-year amount + step growth).</summary>
public sealed record Method14Detail
{
    [JsonPropertyName("firstYearAmt")]
    public decimal FirstYearAmt { get; init; }

    [JsonPropertyName("increaseRatePct")]
    public decimal IncreaseRatePct { get; init; }

    [JsonPropertyName("increaseRateYrs")]
    public int IncreaseRateYrs { get; init; }

    // Table (year-indexed arrays)
    [JsonPropertyName("increaseRates")]
    public decimal[] IncreaseRates { get; init; } = [];

    [JsonProperty("startIn")]
    public int StartIn { get; init; }
}
