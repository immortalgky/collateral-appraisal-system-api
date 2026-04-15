using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 01 — Room Income Per Day (per room type × saleable area × days × occupancy rate).</summary>
public sealed record Method01Detail
{
    [JsonPropertyName("roomDetails")]
    public RoomDetail[] RoomDetails { get; init; } = [];

    [JsonPropertyName("sumRoomIncome")]
    public decimal SumRoomIncome { get; init; }

    [JsonPropertyName("sumSaleableArea")]
    public decimal SumSaleableArea { get; init; }

    [JsonPropertyName("sumTotalRoomIncome")]
    public decimal SumTotalRoomIncome { get; init; }

    [JsonPropertyName("avgRoomRate")]
    public decimal AvgRoomRate { get; init; }

    [JsonPropertyName("totalSaleableArea")]
    public decimal TotalSaleableArea { get; init; }

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
    [JsonPropertyName("saleableArea")]
    public decimal[] SaleableArea { get; init; } = [];

    [JsonPropertyName("occupancyRate")]
    public decimal[] OccupancyRate { get; init; } = [];

    [JsonPropertyName("totalSaleableAreaDeductByOccRate")]
    public decimal[] TotalSaleableAreaDeductByOccRate { get; init; } = [];

    [JsonPropertyName("roomRateIncrease")]
    public decimal[] RoomRateIncrease { get; init; } = [];

    [JsonPropertyName("avgDailyRate")]
    public decimal[] AvgDailyRate { get; init; } = [];

    [JsonPropertyName("roomIncome")]
    public decimal[] RoomIncome { get; init; } = [];

    public sealed record RoomDetail
    {
        [JsonPropertyName("roomType")]
        public string? RoomType { get; init; }

        [JsonPropertyName("roomTypeOther")]
        public string? RoomTypeOther { get; init; }

        [JsonPropertyName("roomIncome")]
        public decimal RoomIncome { get; init; }

        [JsonPropertyName("saleableArea")]
        public decimal SaleableArea { get; init; }

        [JsonPropertyName("totalRoomIncome")]
        public decimal TotalRoomIncome { get; init; }
    }
}
