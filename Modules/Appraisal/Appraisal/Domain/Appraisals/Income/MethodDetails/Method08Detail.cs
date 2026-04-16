using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 08 — F&amp;B Expense Per Room Per Day.</summary>
public sealed record Method08Detail
{
    [JsonPropertyName("firstYearAmt")]
    public decimal FirstYearAmt { get; init; }

    [JsonPropertyName("increaseRatePct")]
    public decimal IncreaseRatePct { get; init; }

    [JsonPropertyName("increaseRateYrs")]
    public int IncreaseRateYrs { get; init; }

    // Table (year-indexed arrays)
    [JsonPropertyName("increaseRate")]
    public decimal[] IncreaseRate { get; init; } = [];

    [JsonPropertyName("totalFoodAndBeveragePerRoomPerDay")]
    public decimal[] TotalFoodAndBeveragePerRoomPerDay { get; init; } = [];

    [JsonPropertyName("totalFoodAndBeveragePerRoomPerYear")]
    public decimal[] TotalFoodAndBeveragePerRoomPerYear { get; init; } = [];
}
