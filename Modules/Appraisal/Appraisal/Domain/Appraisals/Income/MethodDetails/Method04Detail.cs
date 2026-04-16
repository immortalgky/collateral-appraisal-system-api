using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 04 — Room Income With Growth By Occupancy Rate.</summary>
public sealed record Method04Detail
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

    [JsonPropertyName("occupancyRateFirstYearPct")]
    public decimal OccupancyRateFirstYearPct { get; init; }

    [JsonPropertyName("occupancyRatePct")]
    public decimal OccupancyRatePct { get; init; }

    [JsonPropertyName("occupancyRateYrs")]
    public int OccupancyRateYrs { get; init; }

    // Table (year-indexed arrays)
    [JsonPropertyName("occupancyRate")]
    public decimal[] OccupancyRate { get; init; } = [];

    [JsonPropertyName("roomRateIncrease")]
    public decimal[] RoomRateIncrease { get; init; } = [];

    [JsonPropertyName("roomIncomeAdjustedValuedByGrowthRates")]
    public decimal[] RoomIncomeAdjustedValuedByGrowthRates { get; init; } = [];

    [JsonPropertyName("roomIncome")]
    public decimal[] RoomIncome { get; init; } = [];
}
