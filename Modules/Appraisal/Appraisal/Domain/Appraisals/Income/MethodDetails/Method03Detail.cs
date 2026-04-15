using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 03 — Room Income With Growth (first-year amount × compounding step rate).</summary>
public sealed record Method03Detail
{
    [JsonPropertyName("saleableArea")]
    public decimal SaleableArea { get; init; }

    [JsonPropertyName("totalNumberOfSaleableArea")]
    public decimal TotalNumberOfSaleableArea { get; init; }

    [JsonPropertyName("remark")]
    public string Remark { get; init; } = string.Empty;

    [JsonPropertyName("firstYearAmt")]
    public decimal FirstYearAmt { get; init; }

    [JsonPropertyName("increaseRatePct")]
    public decimal IncreaseRatePct { get; init; }

    [JsonPropertyName("increaseRateYrs")]
    public int IncreaseRateYrs { get; init; }

    // Table (year-indexed arrays)
    [JsonPropertyName("roomRateIncrease")]
    public decimal[] RoomRateIncrease { get; init; } = [];

    [JsonPropertyName("roomIncome")]
    public decimal[] RoomIncome { get; init; } = [];
}
