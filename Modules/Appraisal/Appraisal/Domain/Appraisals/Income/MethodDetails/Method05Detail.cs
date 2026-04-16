using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 05 — Rental Income Per Month (monthly × 12).</summary>
public sealed record Method05Detail
{
    [JsonPropertyName("roomDetails")]
    public RoomDetail RoomDetails { get; init; } = new();

    [JsonPropertyName("sumSaleableArea")]
    public decimal SumSaleableArea { get; init; }

    [JsonPropertyName("sumRoomIncomePerMonth")]
    public decimal SumRoomIncomePerMonth { get; init; }

    [JsonPropertyName("sumRoomIncomePerYear")]
    public decimal SumRoomIncomePerYear { get; init; }

    [JsonPropertyName("totalSaleableArea")]
    public decimal TotalSaleableArea { get; init; }

    [JsonPropertyName("increaseRatePct")]
    public decimal IncreaseRatePct { get; init; }

    [JsonPropertyName("increaseRateYrs")]
    public int IncreaseRateYrs { get; init; }

    // Table (year-indexed arrays)
    [JsonPropertyName("roomRateIncrease")]
    public decimal[] RoomRateIncrease { get; init; } = [];

    [JsonPropertyName("roomIncome")]
    public decimal[] RoomIncome { get; init; } = [];

    public sealed record RoomDetail
    {
        [JsonPropertyName("roomType")]
        public string RoomType { get; init; } = string.Empty;

        [JsonPropertyName("roomTypeOther")]
        public string? RoomTypeOther { get; init; }

        [JsonPropertyName("roomIncome")]
        public decimal RoomIncome { get; init; }

        [JsonPropertyName("saleableArea")]
        public decimal SaleableArea { get; init; }

        [JsonPropertyName("totalRoomIncomePerMonth")]
        public decimal TotalRoomIncomePerMonth { get; init; }

        [JsonPropertyName("totalRoomIncomePerYear")]
        public decimal TotalRoomIncomePerYear { get; init; }
    }
}
